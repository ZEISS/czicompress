// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using netczicompressTests.Customizations;

using netczicompressTests.Models.Mocks;

/// <summary>
/// Tests for <see cref="MultiThreadedFolderCompressor"/>.
/// </summary>
public class MultiThreadedFolderCompressorTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenProcessorNeedsOutputDirs_RunCreatesOutputDirs(bool needsOutputDir)
    {
        // ARRANGE
        var processor = Mock.Of<IFileProcessor>(x => x.NeedsExistingOutputDirectory == needsOutputDir);
        var ff = new FoldersAndFiles(
            @"C:\inputDir",
            @"X:\outputDir",
            @"C:\inputDir\subDir\image-1.czi",
            @"C:\inputDir\subDir\image-2.czi",
            @"X:\outputDir\blabla.text");

        var sut = CreateSut((_, _) => processor);

        var run = sut.PrepareNewRun(
            ff.GetFolderCompressorParameters(),
            CancellationToken.None);

        // ACT
        Task t = run.Output.ToTask();
        run.Start();
        await t;

        // ASSERT
        var fs = ff.FileSystem;
        var outputSubDir = fs.DirectoryInfo.New(
            fs.Path.Combine(ff.OutputDir.FullName, "subDir"));
        outputSubDir.Exists.Should().Be(needsOutputDir);
    }

    [Theory]
    [CorrectValueObjectAutoData]
    public async Task WhenRunCompletes_ThenProcessFileHasBeenCalledWithCorrectFileArgs(CompressionMode mode, ExecutionOptions executionOptions, ProcessingOptions processingOptions)
    {
        var ff = new FoldersAndFiles(
            @"C:\inputDir",
            @"D:\outputDir",
            @"C:\inputDir\subDir\image-1.czi",
            @"C:\inputDir\subDir\image-2.czi",
            @"C:\inputDir\subDir\image-3.czi",
            @"C:\inputDir\subDir2\image-4.czi",
            @"C:\inputDir\subDir3\image-5.czi",
            @"C:\inputDir\subDir4\ € 123,;!.czi",
            @"C:\inputDir\subDir4\image-6.tiff",
            @"C:\inputDir\subDir\image-7.czi",
            @"D:\outputDir\blabla.czi");

        await WhenRunCompletes_ThenProcessFileHasBeenCalledWithCorrectFileArgsImpl(mode, executionOptions, processingOptions, ff);
    }

    [Theory]
    [CorrectValueObjectAutoData]
    public async Task WhenRunCompletes_ThenProgressHasBeenReportedForAllFiles(
        CompressionMode mode,
        ExecutionOptions executionOptions,
        ProcessingOptions processingOptions)
    {
        // ARRANGE
        int ProgressForFile(string inputPath) => 1 + (Math.Abs(inputPath.GetHashCode()) % 99);

        var progressRecorder = new ProgressRecorder();
        var processorFactory = CreateProcessorFactory(
            mode,
            processingOptions,
            onMockCreated: m => m.WithProcessFile((inputPath, _, reportProgress, _) =>
                reportProgress(ProgressForFile(inputPath))));

        var ff = new FoldersAndFiles(
            @"C:\inputDir",
            @"C:\outputDir",
            @"C:\inputDir\subDir\image.czi",
            @"C:\inputDir\subDir\image-2.czi",
            @"C:\inputDir\subDir\image-3.czi",
            @"C:\inputDir\subDir2\image.czi",
            @"C:\inputDir\subDir3\image-5.czi",
            @"C:\inputDir\subDir4\image-6.czi",
            @"C:\inputDir\subDir\image-7.czi",
            @"C:\outputDir\blabla.text");
        var czis = ff.InitialFiles.Where(f => f.EndsWith(".czi")).ToArray();

        var sut = CreateSut(processorFactory);

        var run = sut.PrepareNewRun(
            ff.GetFolderCompressorParameters(mode, executionOptions, processingOptions),
            CancellationToken.None);

        run
            .Output
            .OfType<CompressorMessage.FileStarting>()
            .Subscribe(progressRecorder);

        // ACT
        Task t = run.Output.ToTask();
        run.Start();
        await t;

        // ASSERT
        progressRecorder.ProgressValuesByFile.Count.Should().Be(czis.Length);
        foreach (var file in czis)
        {
            progressRecorder.ProgressValuesByFile[file]
                .Should()
                .BeEquivalentTo(
                    new[] { 0, ProgressForFile(file), 100 });
        }
    }

    [Theory]
    [CorrectValueObjectAutoData]
    public async Task WhenRunCompletes_ThenHasNotProcessedAnyFilesCreatedDuringTheRun(
        CompressionMode mode, ExecutionOptions executionOptions, ProcessingOptions processingOptions)
    {
        // Now outputDir is a subdirectory of inputDir. Files created there must not be processed.
        var ff = new FoldersAndFiles(
            @"C:\inputDir",
            @"C:\inputDir\subDir4\outputDir",
            @"C:\inputDir\subDir\image-1.czi",
            @"C:\inputDir\subDir\image-2.czi",
            @"C:\inputDir\subDir\image-3.czi",
            @"C:\inputDir\subDir2\image-4.czi",
            @"C:\inputDir\subDir3\image-5.czi",
            @"C:\inputDir\subDir4\image-6.czi",
            @"C:\inputDir\subDir\image-7.czi",
            @"D:\outputDir\blabla.text");

        void ConfigureProcessor(Mock<IFileProcessor> mock)
        {
            mock.WithProcessFile((inp, outp, _, _) => ff.FileSystem.File.Copy(inp, outp));
            mock.SetupGet(p => p.NeedsExistingOutputDirectory).Returns(true);
        }

        await WhenRunCompletes_ThenProcessFileHasBeenCalledWithCorrectFileArgsImpl(mode, executionOptions, processingOptions, ff, ConfigureProcessor, true);
    }

    [Theory]
    [ValidInlineAutoData(true)]
    [ValidInlineAutoData(false)]
    public async Task WhenCanceled_ThenDisposesAllProcessorsAndThrows(
        bool processFileThrows,
        CompressionMode mode,
        ExecutionOptions executionOptions,
        ProcessingOptions processingOptions)
    {
        var ff = new FoldersAndFiles(
            @"C:\inputDir",
            @"C:\outputDir",
            @"C:\inputDir\subDir\image.czi",
            @"C:\inputDir\subDir\image-2.czi",
            @"C:\inputDir\subDir\image-3.czi",
            @"C:\inputDir\subDir2\image.czi",
            @"C:\inputDir\subDir3\image-5.czi",
            @"C:\inputDir\subDir4\image-6.czi",
            @"C:\inputDir\subDir\image-7.czi",
            @"C:\outputDir\blabla.text");

        // Auto-data does not work here without adaption.
        // In the gitlab runners the maximum thread number is 2, on local machines there might be 28 threads or more
        // available.
        // We need to bound the number of threads by the number of czi-files, mind that one of the initial files is no czi.
        var numOfCziFiles = ff.InitialFiles.Length - 1;
        executionOptions = new ExecutionOptions(new ThreadCount()
            { Value = Math.Min(executionOptions.ThreadCount.Value, numOfCziFiles) });

        // ARRANGE
        using var cts = new CancellationTokenSource();
        ConcurrentBag<Mock<IFileProcessor>> processors = new();
        bool disposedDuringProcessing = false;
        var processorFactory = CreateProcessorFactory(mode, processingOptions, onMockCreated: p =>
        {
            processors.Add(p);
            p.WithProcessFile((_, _, _, cancellationToken) =>
            {
                cancellationToken.WaitHandle.WaitOne();
                Thread.Sleep(20);
                if (p.Invocations.Any(inv => inv.Method.Name == nameof(IDisposable.Dispose)))
                {
                    disposedDuringProcessing = true;
                }

                if (processFileThrows)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            });

            if (processors.Count == executionOptions.ThreadCount.Value)
            {
                cts.Cancel();
            }
        });

        var sut = CreateSut(processorFactory);

        var run = sut.PrepareNewRun(
            ff.GetFolderCompressorParameters(mode, executionOptions, processingOptions),
            cts.Token);

        // ACT
        Task t = run.Output.ToTask();
        run.Start();
        Func<Task> act = () => t;
        await act.Should().ThrowAsync<OperationCanceledException>();

        // ASSERT
        disposedDuringProcessing.Should().BeFalse();
        processors
            .CheckCount(maxCount: executionOptions.ThreadCount.Value)
            .CheckDisposed();
    }

    [Theory]
    [CorrectValueObjectAutoData]
    public async Task WhenNoTemporaryOutputFileCouldBeCreated_ThenProducesExpectedMessages(
        CompressionMode mode,
        ProcessingOptions processingOptions)
    {
        // ARRANGE
        // this does not work on my local machine with other tested values than 1.
        ExecutionOptions executionOptions = new ExecutionOptions(new ThreadCount() { Value = 1 });
        var ff = new FoldersAndFiles(
                    @"C:\inputDir",
                    @"C:\outputDir",
                    true,
                    @"C:\inputDir\image.czi",
                    @"C:\outputDir\image.czi~",
                    @"C:\outputDir\image.czi~1",
                    @"C:\outputDir\image.czi~2",
                    @"C:\outputDir\image.czi~3",
                    @"C:\outputDir\subDir\image.czi~4");

        var processorFactory = CreateProcessorFactory(
            mode,
            processingOptions,
            onMockCreated: m => { });

        var sut = CreateSut(processorFactory);

        var run = sut.PrepareNewRun(
            ff.GetFolderCompressorParameters(mode, executionOptions, processingOptions),
            CancellationToken.None);

        var task = run.Output.ToTask();
        var recorder = new Recorder<CompressorMessage.FileFinished>();
        using var subscription = run.Output
            .OfType<CompressorMessage.FileFinished>()
            .Subscribe(recorder);

        // ACT
        run.Start();
        await task;

        // ASSERT
        CompressorMessage.FileFinished GetExpectation(IFileInfo inputInfo)
        {
            var filename = FoldersAndFiles.ToPlatformPath(@"C:\outputDir\image.czi~3");
            var error = $@"Could not delete existing temporary file {filename}.";
            long expectedOutputSize = 0;

            return new CompressorMessage.FileFinished(
                    inputInfo,
                    inputInfo.Length,
                    expectedOutputSize,
                    null,
                    error);
        }

        var inputFileName = FoldersAndFiles.ToPlatformPath(@"C:\inputDir\image.czi");
        var inputFile = ff!.FileSystem.FileInfo.New(inputFileName);

        var expectation = GetExpectation(inputFile);

        var actualAsStrings = from item in recorder.Data select item.ToString();
        var actualAsStringsWithoutTimeElapsed = actualAsStrings.Select(x => RemoveTimeStamp(x));
        Collection<string> expectationAsStrings = new();
        expectationAsStrings.Add(expectation.ToString());

        // TODO: debug this, this seems to fail sometimes as the order of files/outputs seems not to be fixed.
        actualAsStringsWithoutTimeElapsed.Should().BeEquivalentTo(
            expectationAsStrings,
            compare => compare.WithoutStrictOrdering());
    }

    [Theory]
    [CorrectValueObjectAutoData]
    public async Task WhenOneOutputFileExists_ThenProducesExpectedMessages(
        CompressionMode mode,
        ProcessingOptions processingOptions)
    {
        // ARRANGE
        // this does not work on my local machine with other tested values than 1.
        ExecutionOptions executionOptions = new ExecutionOptions(new ThreadCount() { Value = 1 });
        var ff = new FoldersAndFiles(
                    @"C:\inputDir",
                    @"C:\outputDir",
                    @"C:\inputDir\subDir\image.czi",
                    @"C:\inputDir\subDir\image-3.czi",
                    @"C:\inputDir\subDir2\image.czi",
                    @"C:\inputDir\subDir3\image-6.czi",
                    @"C:\inputDir\subDir4\image-6.czi",
                    @"C:\outputDir\subDir\image-3.czi");

        static IOException? GetErrorForFile(string path) =>
            path.EndsWith("6.czi") ? new IOException("FooBar " + path) : null;

        var processorFactory = CreateProcessorFactory(
            mode,
            processingOptions,
            onMockCreated: m =>
            {
                m.WithProcessFile(
                (inputPath, outputPath, _, _) =>
                {
                    var ex = GetErrorForFile(inputPath);
                    if (ex != null)
                    {
                        throw ex;
                    }

                    lock (ff.FileSystem)
                    {
                        ff.FileSystem.File.AppendAllText(
                            outputPath,
                            inputPath + inputPath);
                    }
                });
                m.SetupGet(x => x.NeedsExistingOutputDirectory).Returns(true);
            });

        var sut = CreateSut(processorFactory);

        var run = sut.PrepareNewRun(
            ff.GetFolderCompressorParameters(mode, executionOptions, processingOptions),
            CancellationToken.None);

        var task = run.Output.ToTask();
        var recorder = new Recorder<CompressorMessage.FileFinished>();
        using var subscription = run.Output
            .OfType<CompressorMessage.FileFinished>()
            .Subscribe(recorder);

        // ACT
        run.Start();
        await task;

        // ASSERT
        CompressorMessage.FileFinished GetExpectation(string inputFile)
        {
            var fileInfo = ff!.FileSystem.FileInfo.New(inputFile);
            var error = GetErrorForFile(inputFile)?.Message;
            long expectedOutputSize = error == null ? fileInfo.Length * 2 : 0;

            if (inputFile.EndsWith("image-3.czi"))
            {
                // output file exists
                var outputFile = inputFile.Replace(ff.InputDir.FullName, ff.OutputDir.FullName);
                error = $"Output file {outputFile} already exists.";
                expectedOutputSize = fileInfo.Length + 1; // unchanged from initial file
            }

            return new CompressorMessage.FileFinished(
                    fileInfo,
                    fileInfo.Length,
                    expectedOutputSize,
                    null,
                    error);
        }

        var expectation = from path in ff.InitialFiles
                          where path.EndsWith(".czi") && path.StartsWith(ff.InputDir.FullName)
                          select GetExpectation(path);

        var actualAsStrings = from item in recorder.Data select item.ToString();
        var actualAsStringsWithoutTimeElapsed = actualAsStrings.Select(x => RemoveTimeStamp(x));
        var expectationAsStrings = from item in expectation select item.ToString();

        // TODO: debug this, this seems to fail sometimes as the order of files/outputs seems not to be fixed.
        actualAsStringsWithoutTimeElapsed.Should().BeEquivalentTo(
            expectationAsStrings,
            compare => compare.WithoutStrictOrdering());
    }

    private static string RemoveTimeStamp(string target)
    {
        const string timeElapsedPattern = $"\\b{nameof(CompressorMessage.FileFinished.TimeElapsed)}[^\\r\\n,]*,";
        const string timeElapsedReplacement = $"{nameof(CompressorMessage.FileFinished.TimeElapsed)} = ,";
        return Regex.Replace(target, timeElapsedPattern, timeElapsedReplacement);
    }

    private static async Task WhenRunCompletes_ThenProcessFileHasBeenCalledWithCorrectFileArgsImpl(
        CompressionMode mode,
        ExecutionOptions executionOptions,
        ProcessingOptions processingOptions,
        FoldersAndFiles ff,
        Action<Mock<IFileProcessor>>? configureProcessor = null,
        bool forceLazyFileSystemEnumeration = false)
    {
        // ARRANGE
        ConcurrentBag<Mock<IFileProcessor>> processors = new();
        var processorFactory = CreateProcessorFactory(
            mode,
            processingOptions,
            onMockCreated: p =>
            {
                processors.Add(p);
                configureProcessor?.Invoke(p);
            });
        string GetExpectedOutputPath(string f) => f.Replace(ff.InputDir.FullName, ff.OutputDir.FullName);

        MultiThreadedFolderCompressor sut;
        if (forceLazyFileSystemEnumeration)
        {
            sut = new LazyEnumerateCziSut(processorFactory)
            {
                MatchExtensionCasing = MatchCasing.PlatformDefault, // CaseInsensitive is currently not supported by MockFileSystem
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System, // AttributesToSkip is currently not supported by MockFileSystem
            };
        }
        else
        {
            sut = CreateSut(processorFactory);
        }

        var run = sut.PrepareNewRun(
            ff.GetFolderCompressorParameters(mode, executionOptions, processingOptions),
            CancellationToken.None);

        // ACT
        Task t = run.Output.ToTask();
        run.Start();
        await t;

        // ASSERT
        processors
            .CheckCount(maxCount: executionOptions.ThreadCount.Value)
            .CheckDisposed();

        var actualFileNamePairs =
            from p in processors
            from argsPair in p.GetProcessedFileArgs()
            select argsPair;

        var expectedFileNamePairs =
            from f in ff.InitialFiles
            where f.EndsWith(".czi") && f.StartsWith(ff.InputDir.FullName)
            select (f, GetExpectedOutputPath(f) + "~");

        actualFileNamePairs.Should().BeEquivalentTo(
            expectedFileNamePairs,
            compare => compare.WithoutStrictOrdering());
    }

    private static MultiThreadedFolderCompressor CreateSut(
        CreateProcessor processorFactory,
        int maxTriesToCreateTemporaryFiles = 4)
    {
        var sut = new MultiThreadedFolderCompressor(
            processorFactory,
            new FileProcessingFailedHandler(),
            maxTriesToCreateTemporaryFiles)
        {
            MatchExtensionCasing =
                MatchCasing.PlatformDefault, // CaseInsensitive is currently not supported by MockFileSystem
            AttributesToSkip = FileAttributes.System | FileAttributes.Hidden, // AttributesToSkip is not supported by MockFileSystem
        };
        return sut;
    }

    private static CreateProcessor CreateProcessorFactory(
        CompressionMode expectedMode,
        ProcessingOptions expectedProcessingOptions,
        Action<Mock<IFileProcessor>>? onMockCreated = null)
    {
        // Strict mock behavior checks that we are receiving the correct mode argument.
        var createProcessor = new Mock<CreateProcessor>(MockBehavior.Strict);
        createProcessor.Setup(x => x.Invoke(expectedMode, expectedProcessingOptions)).Returns<CompressionMode, ProcessingOptions>(
            (_, _) =>
            {
                var p = new Mock<IFileProcessor>().WithProcessFile((_, _, _, c) => c.ThrowIfCancellationRequested());

                onMockCreated?.Invoke(p);

                return p.Object;
            });

        return createProcessor.Object;
    }

    private class FoldersAndFiles
    {
        public FoldersAndFiles(string inputDir, string outputDir, params string[] files)
        : this(inputDir, outputDir, false, files)
        {
        }

        public FoldersAndFiles(string inputDir, string outputDir, bool allFilesReadonly, params string[] files)
        {
            this.InitialFiles = files.Select(ToPlatformPath).ToArray();
            this.FileSystem = CreateFileSystem(this.InitialFiles, allFilesReadonly);
            this.InputDir = this.FileSystem.DirectoryInfo.New(ToPlatformPath(inputDir));
            this.OutputDir = this.FileSystem.DirectoryInfo.New(ToPlatformPath(outputDir));
        }

        public IFileSystem FileSystem { get; }

        public string[] InitialFiles { get; }

        public IDirectoryInfo InputDir { get; }

        public IDirectoryInfo OutputDir { get; }

        public static string ToPlatformPath(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return path;
            }

            var posixPath = path.Replace('\\', '/');
            posixPath = posixPath.Replace(":", string.Empty);
            posixPath = "/cygdrive/" + posixPath;
            return posixPath;
        }

        public FolderCompressorParameters GetFolderCompressorParameters(
            CompressionMode mode = CompressionMode.CompressUncompressed,
            ExecutionOptions? executionOptions = null,
            ProcessingOptions? processingOptions = null,
            bool recursive = true)
        {
            processingOptions ??= new ProcessingOptions(CompressionLevel.Default);
            executionOptions ??= new ExecutionOptions(ThreadCount.Default);
            return new FolderCompressorParameters(
                this.InputDir,
                this.OutputDir,
                recursive,
                mode,
                executionOptions,
                processingOptions);
        }

        private static MockFileSystem CreateFileSystem(string[] files, bool allFilesReadonly = false)
        {
            var filesDict = files.ToDictionary(f => f, f => new MockFileData(f));
            if (allFilesReadonly)
            {
                foreach (var file in filesDict.Values)
                {
                    file.Attributes = FileAttributes.ReadOnly;
                }
            }

            var result = new MockFileSystem(filesDict);
            return result;
        }
    }

    // Overrides EnumerateCzisAsync to first return files and then look into subdirectories.
    // This is necessary when we want to test that output files placed into a subdirectory
    // of the input directory are skipped. It seems that MockFileSystem collects all files
    // immediately (in GetEnumerator() not in MoveNext()).
    private class LazyEnumerateCziSut : MultiThreadedFolderCompressor
    {
        public LazyEnumerateCziSut(CreateProcessor createProcessor)
        : base(createProcessor, new FileProcessingFailedHandler())
        {
        }

        protected override IAsyncEnumerable<IFileInfo> EnumerateCzisAsync(IDirectoryInfo folder, bool recursive, CancellationToken token)
        {
            return recursive
                ? LazyEnumerateCzisAsync(folder, token)
                : base.EnumerateCzisAsync(folder, false, token);

            async IAsyncEnumerable<IFileInfo> LazyEnumerateCzisAsync(
                IDirectoryInfo folder,
                [EnumeratorCancellation] CancellationToken token)
            {
                await foreach (var item in base.EnumerateCzisAsync(folder, false, token)
                    .WithCancellation(token))
                {
                    yield return item;
                }

                foreach (var dir in folder.EnumerateDirectories())
                {
                    await foreach (var item in LazyEnumerateCzisAsync(dir, token)
                        .WithCancellation(token))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}
