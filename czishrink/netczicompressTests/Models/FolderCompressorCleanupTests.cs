// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Runtime.CompilerServices;

/// <summary>Checks exception precedence and processor lifetime during cleanup.</summary>
public class FolderCompressorCleanupTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CleanupWaitsForWorkersAndPreservesThePrimaryFailure(bool enumerationFails, bool workerFails)
    {
        var fs = new MockFileSystem();
        string input = fs.Path.GetFullPath("input");
        string output = fs.Path.GetFullPath("output");
        string source = fs.Path.Combine(input, "image.czi");
        fs.AddFile(source, new MockFileData("unchanged source"));
        fs.File.SetCreationTimeUtc(source, DateTime.UtcNow.AddDays(-1));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enumerationReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var enumerationFailure = enumerationFails ? new UnauthorizedAccessException("Original enumeration failure") : null;
        var workerFailure = new InvalidOperationException("Worker failure");
        bool workerFinished = false;
        bool disposedDuringWork = false;
        var processor = new Mock<IFileProcessor>();
        processor.SetupGet(value => value.NeedsExistingOutputDirectory).Returns(false);
        processor.Setup(value => value.ProcessFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReportProgress>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                started.SetResult();
                release.Task.GetAwaiter().GetResult();
                workerFinished = true;
                if (workerFails)
                {
                    throw workerFailure;
                }
            });
        processor.Setup(value => value.Dispose()).Callback(() => disposedDuringWork = !workerFinished);
        var compressor = new ControlledEnumeratorCompressor(
            (_, _) => processor.Object, new[] { fs.FileInfo.New(source) }, started.Task, enumerationReached, enumerationFailure);
        var parameters = new FolderCompressorParameters(
            fs.DirectoryInfo.New(input),
            fs.DirectoryInfo.New(output),
            false,
            CompressionMode.CompressAll,
            new ExecutionOptions(new ThreadCount { Value = 1 }),
            new ProcessingOptions(CompressionLevel.Default));
        var run = compressor.PrepareNewRun(parameters, CancellationToken.None);
        Task completion = run.Output.ToTask();
        run.Start();
        try
        {
            await enumerationReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            completion.IsCompleted.Should().BeFalse();
            processor.Verify(value => value.Dispose(), Times.Never);
            release.SetResult();
            Exception? failure = await Record.ExceptionAsync(() => completion.WaitAsync(TimeSpan.FromSeconds(10)));

            failure.Should().BeSameAs((Exception?)enumerationFailure ?? workerFailure);
            processor.Verify(value => value.Dispose(), Times.Once);
            disposedDuringWork.Should().BeFalse();
            fs.File.ReadAllText(source).Should().Be("unchanged source");
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [TwoWorkerTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EveryProcessorIsDisposedAndDisposalErrorsDoNotReplaceEarlierErrors(bool enumerationFails)
    {
        var fs = new MockFileSystem();
        string input = fs.Path.GetFullPath("input");
        string output = fs.Path.GetFullPath("output");
        var files = new[] { "first.czi", "second.czi" }.Select(name => fs.Path.Combine(input, name)).ToArray();
        foreach (var file in files)
        {
            fs.AddFile(file, new MockFileData("unchanged source"));
            fs.File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-1));
        }

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var primary = enumerationFails ? new UnauthorizedAccessException("Enumeration failed") : null;
        int startedCount = 0;
        var processors = Enumerable.Range(0, 2).Select(_ => new Mock<IFileProcessor>()).ToArray();
        foreach (var processor in processors)
        {
            processor.SetupGet(value => value.NeedsExistingOutputDirectory).Returns(false);
            processor.Setup(value => value.ProcessFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReportProgress>(), It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    if (Interlocked.Increment(ref startedCount) == 2)
                    {
                        started.TrySetResult();
                    }

                    release.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                });
            processor.Setup(value => value.Dispose()).Throws(new IOException("Disposal failed"));
        }

        int created = 0;
        var compressor = new ControlledEnumeratorCompressor(
            (_, _) => processors[created++].Object, files.Select(fs.FileInfo.New).ToArray(), started.Task, reached, primary);
        var parameters = new FolderCompressorParameters(
            fs.DirectoryInfo.New(input),
            fs.DirectoryInfo.New(output),
            false,
            CompressionMode.CompressAll,
            new ExecutionOptions(new ThreadCount { Value = 2 }),
            new ProcessingOptions(CompressionLevel.Default));
        var run = compressor.PrepareNewRun(parameters, CancellationToken.None);
        Task completion = run.Output.ToTask();
        run.Start();
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            foreach (var processor in processors)
            {
                processor.Verify(value => value.Dispose(), Times.Never);
            }

            release.SetResult();
            var error = await Record.ExceptionAsync(() => completion.WaitAsync(TimeSpan.FromSeconds(10)));
            if (enumerationFails)
            {
                error.Should().BeSameAs(primary);
            }
            else
            {
                error.Should().BeOfType<AggregateException>().Which.InnerExceptions.Should().HaveCount(2);
            }

            foreach (var processor in processors)
            {
                processor.Verify(value => value.Dispose(), Times.Once);
            }
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [TwoWorkerTheory]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    public async Task CleanupPropagatesAllWorkerFailuresWhenAnyIsFatal(bool fatalFirst, bool wrapped, bool enumerationFails)
    {
        var fs = new MockFileSystem();
        string input = fs.Path.GetFullPath("input");
        var files = new[] { "first.czi", "second.czi" }.Select(name => fs.Path.Combine(input, name)).ToArray();
        foreach (var file in files)
        {
            fs.AddFile(file, new MockFileData("unchanged source"));
            fs.File.SetCreationTimeUtc(file, DateTime.UtcNow.AddDays(-1));
        }

        // Construct a fatal exception object without exhausting memory.
        var fatal = new OutOfMemoryException("Synthetic fatal worker failure");
        Exception fatalFailure = wrapped ? new InvalidOperationException("Wrapped fatal failure", fatal) : fatal;
        var ordinary = new InvalidOperationException("Ordinary worker failure");
        var failures = fatalFirst ? new[] { fatalFailure, ordinary } : new[] { ordinary, fatalFailure };
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int startedCount = 0;
        var processors = failures.Select(failure =>
        {
            var processor = new Mock<IFileProcessor>();
            processor.SetupGet(value => value.NeedsExistingOutputDirectory).Returns(false);
            processor.Setup(value => value.ProcessFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReportProgress>(), It.IsAny<CancellationToken>()))
                .Callback(() =>
                {
                    if (Interlocked.Increment(ref startedCount) == 2)
                    {
                        started.TrySetResult();
                    }

                    release.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
                    throw failure;
                });
            return processor;
        }).ToArray();
        int created = 0;
        var primary = enumerationFails ? new UnauthorizedAccessException("Enumeration failed") : null;
        var compressor = new ControlledEnumeratorCompressor(
            (_, _) => processors[created++].Object, files.Select(fs.FileInfo.New).ToArray(), started.Task, reached, primary);
        var parameters = new FolderCompressorParameters(
            fs.DirectoryInfo.New(input),
            fs.DirectoryInfo.New(fs.Path.GetFullPath("output")),
            false,
            CompressionMode.CompressAll,
            new ExecutionOptions(new ThreadCount { Value = 2 }),
            new ProcessingOptions(CompressionLevel.Default));
        var run = compressor.PrepareNewRun(parameters, CancellationToken.None);
        Task completion = run.Output.ToTask();
        run.Start();
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            release.SetResult();
            var error = await Record.ExceptionAsync(() => completion.WaitAsync(TimeSpan.FromSeconds(10)));
            error.Should().BeOfType<AggregateException>().Which.Flatten().InnerExceptions
                .Should().HaveCount(2).And.Contain(ordinary).And.Contain(fatalFailure);
            foreach (var processor in processors)
            {
                processor.Verify(value => value.Dispose(), Times.Once);
            }

            foreach (var file in files)
            {
                fs.File.ReadAllText(file).Should().Be("unchanged source");
            }
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FinalProgressFailurePreservesWorkerFailureAndStillSurfacesOnSuccess(bool workerFails)
    {
        var fs = new MockFileSystem();
        string input = fs.Path.GetFullPath("input");
        string source = fs.Path.Combine(input, "image.czi");
        fs.AddFile(source, new MockFileData("unchanged source"));
        fs.File.SetCreationTimeUtc(source, DateTime.UtcNow.AddDays(-1));
        var primary = new InvalidOperationException("Worker failed");
        var secondary = new InvalidOperationException("Progress observer failed");
        var processor = new Mock<IFileProcessor>();
        processor.SetupGet(value => value.NeedsExistingOutputDirectory).Returns(false);
        processor.Setup(value => value.ProcessFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReportProgress>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (workerFails)
                {
                    throw primary;
                }
            });
        var compressor = new MultiThreadedFolderCompressor((_, _) => processor.Object, new FileProcessingFailedHandler())
        {
            MatchExtensionCasing = MatchCasing.PlatformDefault,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        };
        var parameters = new FolderCompressorParameters(
            fs.DirectoryInfo.New(input),
            fs.DirectoryInfo.New(fs.Path.GetFullPath("output")),
            false,
            CompressionMode.CompressAll,
            new ExecutionOptions(new ThreadCount { Value = 1 }),
            new ProcessingOptions(CompressionLevel.Default));
        var run = compressor.PrepareNewRun(parameters, CancellationToken.None);
        using var progress = new SerialDisposable();
        using var subscription = run.Output.OfType<CompressorMessage.FileStarting>().Subscribe(
            message => progress.Disposable = message.ProgressPercent.Subscribe(percent =>
            {
                if (percent == 100)
                {
                    throw secondary;
                }
            }),
            _ => { });
        Task completion = run.Output.ToTask();
        run.Start();
        var error = await Record.ExceptionAsync(() => completion.WaitAsync(TimeSpan.FromSeconds(10)));
        error.Should().BeSameAs(workerFails ? primary : secondary);
        processor.Verify(value => value.Dispose(), Times.Once);
    }

    private sealed class TwoWorkerTheoryAttribute : TheoryAttribute
    {
        public TwoWorkerTheoryAttribute()
        {
            if (ThreadCount.Maximum < 2)
            {
                this.Skip = "This scenario requires two concurrent workers.";
            }
        }
    }

    private sealed class ControlledEnumeratorCompressor(
        CreateProcessor factory,
        IFileInfo[] files,
        Task started,
        TaskCompletionSource enumerationReached,
        Exception? enumerationFailure) : MultiThreadedFolderCompressor(factory, new FileProcessingFailedHandler())
    {
        protected override async IAsyncEnumerable<IFileInfo> EnumerateCzisAsync(
            IDirectoryInfo folder, bool recursive, [EnumeratorCancellation] CancellationToken token)
        {
            foreach (var file in files)
            {
                yield return file;
            }

            await started.WaitAsync(TimeSpan.FromSeconds(10), token);
            enumerationReached.SetResult();
            if (enumerationFailure != null)
            {
                throw enumerationFailure;
            }
        }
    }
}
