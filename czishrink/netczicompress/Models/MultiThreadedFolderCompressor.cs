// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A multi-threaded <see cref="IFolderCompressor"/> that uses <see cref="IFileProcessor"/>s to do the actual work.
/// </summary>
public class MultiThreadedFolderCompressor(
        CreateProcessor createProcessor,
        FileProcessingFailedHandler fileProcessingFailedHandler,
        int maxTriesToCreateTemporaryFiles = 100)
    : IFolderCompressor
{
    // Public only for testing. MockFileSystem does not support CaseInsensitive
    public MatchCasing MatchExtensionCasing { get; init; } = MatchCasing.CaseInsensitive;

    // Public only for testing. MockFileSystem does not support AttributesToSkip
    public FileAttributes AttributesToSkip { get; init; } = FileAttributes.Hidden;

    // Public only for testing. MockFileSystem does not support skipping reparse points.
    public FileAttributes OtherFilesAttributesToSkip { get; init; } = FileAttributes.ReparsePoint;

    /// <inheritdoc/>
    public IFolderCompressorRun PrepareNewRun(FolderCompressorParameters parameters, CancellationToken token)
    {
        if (parameters.ProcessingOptions.CopyOtherFiles)
        {
            ProcessingPathValidation.RejectLinkedDirectories(parameters.InputDir);
            ProcessingPathValidation.RejectLinkedDirectories(parameters.OutputDir);
        }

        if (parameters.ProcessingOptions.CopyOtherFiles && SameDirectory(parameters.InputDir, parameters.OutputDir))
        {
            throw new ArgumentException("Input and output folders must be different when copying other files.", nameof(parameters));
        }

        return new AsyncEnumerableToObservableActionAdapter<CompressorMessage>(CoreAsync(token), token);

        async IAsyncEnumerable<CompressorMessage> CoreAsync([EnumeratorCancellation] CancellationToken t = default)
        {
            var startTime = DateTime.UtcNow;

            var (inputDir, outputDir, recursive, mode, executionOptions, processingOptions) = parameters;

            var maxNumberOfThreads = executionOptions.ThreadCount.Value;

            var processors = new List<IFileProcessor>(maxNumberOfThreads);
            using IFileProcessor copyProcessor = mode == CompressionMode.NoOp
                ? new NoOpFileProcessor()
                : new CopyFileProcessor(inputDir.FileSystem);
            var activeTasks = new List<Task<CompressorMessage.FileFinished>>(maxNumberOfThreads);
            bool completedNormally = false;
            using var processorLifetime = Disposable.Create(() => DisposeProcessors(processors, completedNormally));
            try
            {
                var files = processingOptions.CopyOtherFiles
                    ? this.EnumerateAllFilesAsync(inputDir, inputDir, outputDir, recursive, mode != CompressionMode.NoOp, t)
                    : this.EnumerateCziEntriesAsync(inputDir, outputDir, recursive, t);
                await foreach (var entry in files.WithCancellation(t).ConfigureAwait(false))
                {
                    var file = entry.File;
                    if (entry.Error != null)
                    {
                        yield return new CompressorMessage.FileFinished(file, 0, 0, null, entry.Error);
                        continue;
                    }

                    if (file.CreationTimeUtc >= startTime)
                    {
                        // skip files that we may have created ourselves
                        continue;
                    }

                    if (t.IsCancellationRequested)
                    {
                        // Don't ThrowIfCancellationRequested here,
                        // we first need to wait for activeTasks to complete.
                        break;
                    }

                    var thisFile = file;
                    var progressObservable = new BehaviorSubject<int>(0);

                    yield return new CompressorMessage.FileStarting(
                        thisFile,
                        progressObservable.DistinctUntilChanged().AsObservable());

                    IFileInfo outFile = GetOutputFileInfo(inputDir, outputDir, file);

                    if (activeTasks.Count < maxNumberOfThreads)
                    {
                        var processor = createProcessor(mode, processingOptions);
                        processors.Add(processor);
                        activeTasks.Add(Run(processor));
                    }
                    else
                    {
                        int index = await WaitForTaskToComplete(activeTasks).ConfigureAwait(false);
                        yield return await activeTasks[index].ConfigureAwait(false);
                        activeTasks[index].Dispose();
                        activeTasks[index] = Run(processors[index]);
                    }

                    Task<CompressorMessage.FileFinished> Run(IFileProcessor p) => Task.Run(() =>
                    {
                        Exception? processingFailure = null;
                        try
                        {
                            bool isCzi = string.Equals(thisFile.Extension, ".czi", StringComparison.OrdinalIgnoreCase);
                            return this.ProcessFile(
                                isCzi ? p : copyProcessor,
                                thisFile,
                                outFile,
                                isCzi && processingOptions.CopyFailedFiles,
                                progressObservable.OnNext,
                                t);
                        }
                        catch (Exception error)
                        {
                            processingFailure = error;
                            throw;
                        }
                        finally
                        {
                            // Make sure that we deliver 100 percent progress.
                            // This is used by observers to detect completion.
                            try
                            {
                                progressObservable.OnNext(100);
                            }
                            catch (Exception notificationError) when (processingFailure != null && FailureDiagnostics.IsRecoverable(notificationError))
                            {
                                FailureDiagnostics.Record(notificationError, processingFailure);
                            }
                        }
                    });
                }

                // No more new tasks will be started.
                // Now wait for the active tasks to complete.
                while (activeTasks.Count != 0)
                {
                    int index = await WaitForTaskToComplete(activeTasks).ConfigureAwait(false);
                    yield return await activeTasks[index].ConfigureAwait(false);
                    activeTasks[index].Dispose();
                    activeTasks.RemoveAt(index);
                }

                t.ThrowIfCancellationRequested();
                completedNormally = true;
            }
            finally
            {
                // Directory enumeration or creation can fail while files are still being processed.
                // Wait for those tasks before releasing their native processors.
                var allWorkers = Task.WhenAll(activeTasks);
                try
                {
                    await allWorkers.ConfigureAwait(false);
                }
                catch (Exception waitError) when (allWorkers.IsFaulted || FailureDiagnostics.IsRecoverable(waitError))
                {
                    // Await exposes only one failure; another worker may have failed fatally.
                    var workerFailures = allWorkers.Exception ?? waitError;
                    if (!FailureDiagnostics.IsRecoverable(workerFailures))
                    {
                        ExceptionDispatchInfo.Throw(workerFailures);
                    }

                    // Normal worker failures are observed above. This cleanup wait
                    // must not replace the original processing or enumeration error.
                    FailureDiagnostics.Record(workerFailures);
                }
            }
        }
    }

    protected virtual IAsyncEnumerable<IFileInfo> EnumerateCzisAsync(
        IDirectoryInfo folder,
        bool recursive,
        CancellationToken token)
    {
        var opts = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            MatchCasing = this.MatchExtensionCasing,
            RecurseSubdirectories = recursive,
            BufferSize = 1 << 16,
            AttributesToSkip = this.AttributesToSkip,
        };

        return folder.EnumerateFilesAsync("*.czi", opts, token);
    }

    private static void DisposeProcessors(IEnumerable<IFileProcessor> processors, bool throwOnError)
    {
        var disposalErrors = new List<Exception>();
        foreach (var processor in processors)
        {
            try
            {
                processor.Dispose();
            }
            catch (Exception disposalError) when (FailureDiagnostics.IsRecoverable(disposalError))
            {
                disposalErrors.Add(disposalError);
                FailureDiagnostics.Record(disposalError);
            }
        }

        if (throwOnError && disposalErrors.Count != 0)
        {
            throw new AggregateException("One or more file processors could not be disposed.", disposalErrors);
        }
    }

    private static void EnsureParentDirectoryExists(IFileInfo outFile)
    {
        var outputDir = outFile.Directory;
        if (outputDir != null)
        {
            ProcessingPathValidation.RejectLinkedDirectories(outputDir);
        }

        if (outputDir?.Exists == false)
        {
            outputDir.Create();
        }
    }

    private static bool SameDirectory(IDirectoryInfo first, IDirectoryInfo second)
    {
        return string.Equals(
            Path.TrimEndingDirectorySeparator(first.FullName),
            Path.TrimEndingDirectorySeparator(second.FullName),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static async Task<int> WaitForTaskToComplete(IReadOnlyList<Task> tasks)
    {
        while (true)
        {
            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i].IsCompleted)
                {
                    return i;
                }
            }

            await Task.WhenAny(tasks).ConfigureAwait(false);
        }
    }

    private static IFileInfo GetOutputFileInfo(IDirectoryInfo inputDir, IDirectoryInfo outputDir, IFileInfo file)
    {
        var inputFileSystem = inputDir.FileSystem;
        var outputFileSystem = outputDir.FileSystem;
        var relativePath = inputFileSystem.Path.GetRelativePath(inputDir.FullName, file.FullName);
        var outputPath = outputFileSystem.Path.Combine(outputDir.FullName, relativePath);
        var outFile = outputFileSystem.FileInfo.New(outputPath);
        return outFile;
    }

    private async IAsyncEnumerable<IFileInfo> EnumerateCzisOutsideOutputAsync(
        IDirectoryInfo folder,
        IDirectoryInfo outputDir,
        bool recursive,
        [EnumeratorCancellation] CancellationToken token)
    {
        // Own recursion so the scanner never enters our changing staging tree.
        await foreach (var file in this.EnumerateCzisAsync(folder, false, token).ConfigureAwait(false))
        {
            yield return file;
        }

        if (!recursive)
        {
            yield break;
        }

        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            AttributesToSkip = this.AttributesToSkip,
        };
        foreach (var child in folder.EnumerateDirectories("*", options))
        {
            if (SameDirectory(child, outputDir))
            {
                continue;
            }

            await foreach (var file in this.EnumerateCzisOutsideOutputAsync(child, outputDir, true, token).ConfigureAwait(false))
            {
                yield return file;
            }
        }
    }

    private async IAsyncEnumerable<InputEntry> EnumerateCziEntriesAsync(
        IDirectoryInfo folder,
        IDirectoryInfo outputDir,
        bool recursive,
        [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var file in this.EnumerateCzisOutsideOutputAsync(folder, outputDir, recursive, token).ConfigureAwait(false))
        {
            yield return new InputEntry(file);
        }
    }

    private async IAsyncEnumerable<InputEntry> EnumerateAllFilesAsync(
        IDirectoryInfo folder,
        IDirectoryInfo inputDir,
        IDirectoryInfo outputDir,
        bool recursive,
        bool createDirectories,
        [EnumeratorCancellation] CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (SameDirectory(folder, outputDir))
        {
            // Do not copy our own output or reports when the destination is inside the input tree.
            yield break;
        }

        if (createDirectories)
        {
            var relativePath = inputDir.FileSystem.Path.GetRelativePath(inputDir.FullName, folder.FullName);
            var destination = outputDir.FileSystem.DirectoryInfo.New(outputDir.FileSystem.Path.Combine(outputDir.FullName, relativePath));
            string? error = null;
            try
            {
                ProcessingPathValidation.RejectLinkedDirectories(destination);
                destination.Create();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = $"Could not create output folder {destination.FullName}: {ex.Message}";
            }

            if (error != null)
            {
                // Report even empty blocked folders, then let each affected file
                // report its own failure without preventing unrelated siblings.
                yield return new InputEntry(inputDir.FileSystem.FileInfo.New(folder.FullName), error);
            }
        }

        var opts = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            AttributesToSkip = this.OtherFilesAttributesToSkip,
        };
        await foreach (var file in folder.EnumerateFilesAsync("*", opts, token).ConfigureAwait(false))
        {
            yield return new InputEntry(file);
        }

        if (recursive)
        {
            foreach (var directory in folder.EnumerateDirectories("*", opts))
            {
                await foreach (var file in this.EnumerateAllFilesAsync(
                    directory, inputDir, outputDir, true, createDirectories, token).ConfigureAwait(false))
                {
                    yield return file;
                }
            }
        }
    }

    private CompressorMessage.FileFinished ProcessFile(
        IFileProcessor processor,
        IFileInfo inFile,
        IFileInfo outFile,
        bool copyFailedFile,
        ReportProgress reportProgress,
        CancellationToken token)
    {
        TemporaryFile? temporaryFile = null;
        using var temporaryFileLifetime = Disposable.Create(() => temporaryFile?.Dispose());
        bool processedByProcessor = false;
        try
        {
            if (token.IsCancellationRequested)
            {
                return MakeTheReturnValue(new OperationCanceledException().Message);
            }

            if (outFile.Exists)
            {
                return MakeTheReturnValue(
                    errorMessage: $"Output file {outFile} already exists.");
            }

            if (!processor.NeedsExistingOutputDirectory)
            {
                // Dry Run must never allocate or clean up any temporary path.
                var timestamp = Stopwatch.GetTimestamp();
                processor.ProcessFile(inFile.FullName, outFile.FullName, reportProgress, token);
                return MakeTheReturnValue(errorMessage: null, timeElapsed: Stopwatch.GetElapsedTime(timestamp));
            }

            EnsureParentDirectoryExists(outFile);
            temporaryFile = new TemporaryFile(outFile, maxTriesToCreateTemporaryFiles);
            if (temporaryFile.TemporaryFileCreationFailed)
            {
                return MakeTheReturnValue(
                    errorMessage: $"Could not reserve temporary output for {outFile}.");
            }

            var temporaryOutFile = temporaryFile.Info;
            var startTimestamp = Stopwatch.GetTimestamp();
            processor.ProcessFile(inFile.FullName, temporaryOutFile.FullName, reportProgress, token);
            processedByProcessor = true;
            var elapsedTime = Stopwatch.GetElapsedTime(startTimestamp);
            token.ThrowIfCancellationRequested();
            temporaryFile.MoveToOutFileIfExists();
            return MakeTheReturnValue(timeElapsed: elapsedTime, errorMessage: null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            // Remove partially written file
            temporaryFile?.DeleteAllOutFiles();

            if (temporaryFile == null || processedByProcessor || ex is OperationCanceledException)
            {
                return MakeTheReturnValue(errorMessage: ex.Message);
            }

            // We only cover those exceptions, that have been reported when processing the file, i.e. processedByProcessor is false.
            var tempOutFile = temporaryFile.Info;
            return MakeTheReturnValue(
                fileProcessingFailedHandler.FileProcessingFailed(
                    inFile,
                    outFile,
                    tempOutFile,
                    reportProgress,
                    ex.Message,
                    copyFailedFile,
                    token));
        }

        CompressorMessage.FileFinished MakeTheReturnValue(string? errorMessage, TimeSpan? timeElapsed = null)
        {
            long inSize = inFile.Exists ? inFile.Length : 0;
            long outSize = outFile.Exists ? outFile.Length : 0;
            return new CompressorMessage.FileFinished(inFile, inSize, outSize, timeElapsed, errorMessage);
        }
    }

    private sealed record InputEntry(IFileInfo File, string? Error = null);
}
