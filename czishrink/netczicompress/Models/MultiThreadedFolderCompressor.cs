// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A multi-threaded <see cref="IFolderCompressor"/> that uses <see cref="IFileProcessor"/>s to do the actual work.
/// </summary>
public class MultiThreadedFolderCompressor : IFolderCompressor
{
    private readonly CreateProcessor createProcessor;
    private readonly FileProcessingFailedHandler fileProcessingFailedHandler;
    private readonly int maxTriesToCreateTemporaryFile;

    public MultiThreadedFolderCompressor(
        CreateProcessor createProcessor,
        FileProcessingFailedHandler fileProcessingFailedHandler,
        int maxTriesToCreateTemporaryFiles = 100)
    {
        this.createProcessor = createProcessor;
        this.fileProcessingFailedHandler = fileProcessingFailedHandler ?? throw new ArgumentNullException(nameof(fileProcessingFailedHandler));
        this.maxTriesToCreateTemporaryFile = maxTriesToCreateTemporaryFiles;
    }

    // Public only for testing. MockFileSystem does not support CaseInsensitive
    public MatchCasing MatchExtensionCasing { get; init; } = MatchCasing.CaseInsensitive;

    // Public only for testing. MockFileSystem does not support AttributesToSkip
    public FileAttributes AttributesToSkip { get; init; } = FileAttributes.Hidden;

    /// <inheritdoc/>
    public IFolderCompressorRun PrepareNewRun(FolderCompressorParameters parameters, CancellationToken token)
    {
        return new AsyncEnumerableToObservableActionAdapter<CompressorMessage>(CoreAsync(token), token);

        async IAsyncEnumerable<CompressorMessage> CoreAsync([EnumeratorCancellation] CancellationToken t = default)
        {
            var startTime = DateTime.UtcNow;

            var (inputDir, outputDir, recursive, mode, executionOptions, processingOptions) = parameters;

            var maxNumberOfThreads = executionOptions.ThreadCount.Value;

            var processors = new List<IFileProcessor>(maxNumberOfThreads);
            try
            {
                var activeTasks = new List<Task<CompressorMessage.FileFinished>>(maxNumberOfThreads);
                await foreach (var file in this.EnumerateCzisAsync(inputDir, recursive, t).WithCancellation(t).ConfigureAwait(false))
                {
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
                        var processor = this.createProcessor(mode, processingOptions);
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
                                t);
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
                foreach (var processor in processors)
                {
                    processor.Dispose();
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

    private static void EnsureParentDirectoryExists(IFileInfo outFile)
    {
        var outputDir = outFile.Directory;
        if (outputDir?.Exists == false)
        {
            outputDir.Create();
        }
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

    private CompressorMessage.FileFinished ProcessFile(
        IFileProcessor processor,
        IFileInfo inFile,
        IFileInfo outFile,
        bool copyFailedFile,
        ReportProgress reportProgress,
        CancellationToken token)
    {
        TemporaryFile? temporaryFile = null;
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

            if (processor.NeedsExistingOutputDirectory)
            {
                EnsureParentDirectoryExists(outFile);
            }

            temporaryFile = new TemporaryFile(outFile, this.maxTriesToCreateTemporaryFile);
            if (temporaryFile.TemporaryFileCreationFailed)
            {
                var tempOutFile = temporaryFile.Info;
                return MakeTheReturnValue(
                    errorMessage: $"Could not delete existing temporary file {tempOutFile}.");
            }

            var temporaryOutFile = temporaryFile.Info;
            var startTimestamp = Stopwatch.GetTimestamp();
            processor.ProcessFile(inFile.FullName, temporaryOutFile.FullName, reportProgress, token);
            processedByProcessor = true;
            var elapsedTime = Stopwatch.GetElapsedTime(startTimestamp);
            temporaryFile.MoveToOutFileIfExists();
            return MakeTheReturnValue(timeElapsed: elapsedTime, errorMessage: null);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
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
                this.fileProcessingFailedHandler.FileProcessingFailed(
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
}
