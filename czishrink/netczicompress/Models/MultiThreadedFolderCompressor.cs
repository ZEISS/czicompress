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
    public FileAttributes AdditionalFormatsAttributesToSkip { get; init; } = FileAttributes.ReparsePoint;

    /// <inheritdoc/>
    public IFolderCompressorRun PrepareNewRun(FolderCompressorParameters parameters, CancellationToken token)
    {
        if (parameters.ProcessingOptions.HasScientificFormats)
        {
            ProcessingPathValidation.RejectLinkedDirectories(parameters.InputDir);
            ProcessingPathValidation.RejectLinkedDirectories(parameters.OutputDir);
        }

        if (parameters.ProcessingOptions.HasScientificFormats && SameDirectory(parameters.InputDir, parameters.OutputDir))
        {
            throw new ArgumentException("Input and output folders must be different when processing additional formats.", nameof(parameters));
        }

        return new AsyncEnumerableToObservableActionAdapter<CompressorMessage>(CoreAsync(token), token);

        async IAsyncEnumerable<CompressorMessage> CoreAsync([EnumeratorCancellation] CancellationToken t = default)
        {
            var startTime = DateTime.UtcNow;

            var (inputDir, outputDir, recursive, mode, executionOptions, processingOptions) = parameters;

            var maxNumberOfThreads = executionOptions.ThreadCount.Value;

            var processors = new List<IFileProcessor>(maxNumberOfThreads);
            using var processorLifetime = Disposable.Create(() =>
            {
                foreach (var processor in processors)
                {
                    processor.Dispose();
                }
            });
            var activeTasks = new List<Task<CompressorMessage.FileFinished>>(maxNumberOfThreads);
            try
            {
                var files = processingOptions.HasScientificFormats
                    ? this.EnumerateAdditionalFormatsAsync(inputDir, outputDir, recursive, t)
                    : this.EnumerateCzisOutsideOutputAsync(inputDir, outputDir, recursive, t);
                await foreach (var file in files.WithCancellation(t).ConfigureAwait(false))
                {
                    if (!processingOptions.ProcessesExtension(file.Extension))
                    {
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
                        try
                        {
                            return this.ProcessFile(
                                p,
                                thisFile,
                                outFile,
                                processingOptions.CopyFailedFiles,
                                progressObservable.OnNext,
                                t,
                                mode);
                        }
                        finally
                        {
                            // Make sure that we deliver 100 percent progress.
                            // This is used by observers to detect completion.
                            progressObservable.OnNext(100);
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
            }
            finally
            {
                // Directory enumeration or creation can fail while files are still being processed.
                // Wait for those tasks before releasing their native processors.
                await Task.WhenAll(activeTasks).ConfigureAwait(false);
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

    private async IAsyncEnumerable<IFileInfo> EnumerateAdditionalFormatsAsync(
        IDirectoryInfo folder,
        IDirectoryInfo outputDir,
        bool recursive,
        [EnumeratorCancellation] CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (SameDirectory(folder, outputDir))
        {
            // Do not copy our own output or reports when the destination is inside the input tree.
            yield break;
        }

        var opts = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            AttributesToSkip = this.AdditionalFormatsAttributesToSkip,
        };
        await foreach (var file in folder.EnumerateFilesAsync("*", opts, token).ConfigureAwait(false))
        {
            yield return file;
        }

        if (recursive)
        {
            foreach (var directory in folder.EnumerateDirectories("*", opts))
            {
                await foreach (var file in this.EnumerateAdditionalFormatsAsync(
                    directory, outputDir, true, token).ConfigureAwait(false))
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
        CancellationToken token,
        CompressionMode mode)
    {
        TemporaryFile? temporaryFile = null;
        using var temporaryFileLifetime = Disposable.Create(() => temporaryFile?.Dispose());
        bool processedByProcessor = false;
        ProcessingDetails? details = null;
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
            details = (processor as IProcessingDetailsSource)?.LastDetails;
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
            var failure = fileProcessingFailedHandler.FileProcessingFailed(
                    inFile,
                    outFile,
                    tempOutFile,
                    reportProgress,
                    ex.Message,
                    copyFailedFile,
                    token);
            details = fileProcessingFailedHandler.LastDetails;
            return MakeTheReturnValue(failure);
        }

        CompressorMessage.FileFinished MakeTheReturnValue(string? errorMessage, TimeSpan? timeElapsed = null)
        {
            outFile.Refresh();
            long inSize = inFile.Exists ? inFile.Length : 0;
            long outSize = outFile.Exists ? outFile.Length : 0;
            if (details == null && (processor is IProcessingDetailsSource || processor is NoOpFileProcessor))
            {
                var format = inFile.Extension.TrimStart('.').ToUpperInvariant();
                details = new ProcessingDetails(
                    format,
                    mode.ToString(),
                    "Not applied",
                    processor is NoOpFileProcessor ? "Listed only (dry run)" : errorMessage?.Contains("already exists", StringComparison.Ordinal) == true ? "Skipped existing output" : "Failed",
                    "Not verified");
            }

            return new CompressorMessage.FileFinished(inFile, inSize, outSize, timeElapsed, errorMessage)
            {
                Details = details == null ? null : details with { OutputFile = outFile.FullName },
            };
        }
    }
}
