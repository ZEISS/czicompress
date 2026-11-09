// SPDX-FileCopyrightText: 2026 czicompress contributors
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Threading.Tasks;

/// <summary>
/// Tests for copying files alongside compressed CZIs.
/// </summary>
public class CopyOtherFilesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyOtherFiles_PreservesTildeNamedFiles(bool destinationAlreadyExists)
    {
        var fs = new MockFileSystem();
        string input = NewPath("input");
        string output = NewPath("output");
        fs.AddFile(Path.Combine(input, "notes.txt~"), new MockFileData("accompanying tilde file"));
        fs.AddFile(Path.Combine(input, "notes.txt"), new MockFileData("notes"));
        if (destinationAlreadyExists)
        {
            fs.AddFile(Path.Combine(output, "notes.txt~"), new MockFileData("valuable destination"));
        }

        var messages = await Run(fs, input, output);

        fs.File.ReadAllText(Path.Combine(output, "notes.txt~")).Should().Be(
            destinationAlreadyExists ? "valuable destination" : "accompanying tilde file");
        fs.File.ReadAllText(Path.Combine(output, "notes.txt")).Should().Be("notes");
        fs.File.ReadAllText(Path.Combine(input, "notes.txt~")).Should().Be("accompanying tilde file");
        messages.Count(m => m.ErrorMessage != null).Should().Be(destinationAlreadyExists ? 1 : 0);
        fs.Directory.GetFiles(output).Should().HaveCount(2);
        fs.Directory.GetDirectories(output).Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DryRunPreservesAllExistingDestinationFiles(bool copyOtherFiles)
    {
        var fs = new MockFileSystem();
        string input = NewPath("input");
        string output = NewPath("output");
        fs.AddFile(Path.Combine(input, "image.czi"), new MockFileData("original"));
        fs.AddFile(Path.Combine(input, "notes.txt"), new MockFileData("notes"));
        fs.AddFile(Path.Combine(output, "image.czi~"), new MockFileData("valuable image tilde"));
        fs.AddFile(Path.Combine(output, "notes.txt~"), new MockFileData("valuable note tilde"));
        fs.AddFile(Path.Combine(output, "notes.txt~1"), new MockFileData("valuable numbered file"));
        var before = fs.AllFiles.ToDictionary(path => path, fs.File.ReadAllText);

        await Run(fs, input, output, copyOtherFiles: copyOtherFiles, mode: CompressionMode.NoOp);

        fs.AllFiles.ToDictionary(path => path, fs.File.ReadAllText).Should().BeEquivalentTo(before);
        fs.Directory.GetDirectories(output).Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DestinationFolderCollisionIsReportedAndSiblingFilesContinue(bool emptyFolder)
    {
        var fs = new MockFileSystem();
        string input = NewPath("input");
        string output = NewPath("output");
        fs.Directory.CreateDirectory(Path.Combine(input, "TIFF"));
        if (!emptyFolder)
        {
            fs.AddFile(Path.Combine(input, "TIFF", "image.tif"), new MockFileData("pixels"));
        }

        fs.AddFile(Path.Combine(input, "unrelated", "notes.txt"), new MockFileData("sibling notes"));
        fs.AddFile(Path.Combine(output, "TIFF"), new MockFileData("valuable file blocking the folder"));

        var messages = await Run(fs, input, output);

        fs.File.ReadAllText(Path.Combine(output, "TIFF")).Should().Be("valuable file blocking the folder");
        fs.File.ReadAllText(Path.Combine(output, "unrelated", "notes.txt")).Should().Be("sibling notes");
        messages.Should().Contain(m => m.ErrorMessage != null && m.ErrorMessage.Contains("Could not create output folder"));
        if (!emptyFolder)
        {
            messages.Should().Contain(m => m.InputFile.Name == "image.tif" && m.ErrorMessage != null);
        }

        using var text = new StringWriter();
        var csv = new CsvLogFileWriter(text);
        foreach (var message in messages)
        {
            csv.OnNext(message);
        }

        text.ToString().Should().Contain("Could not create output folder").And.Contain("notes.txt");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void LinkedRootOrAncestorIsRejectedBeforeTraversal(bool inputIsLinked, bool ancestorIsLinked)
    {
        var fs = new MockFileSystem();
        string root = NewPath("root");
        var input = fs.Directory.CreateDirectory(Path.Combine(root, "source", "input"));
        var output = fs.Directory.CreateDirectory(Path.Combine(root, "destination", "output"));
        var linked = inputIsLinked ? input : output;
        if (ancestorIsLinked)
        {
            linked = linked.Parent!;
        }

        linked.Attributes |= FileAttributes.ReparsePoint;
        var compressor = new MultiThreadedFolderCompressor((_, _) => new NoOpFileProcessor(), new FileProcessingFailedHandler());
        var parameters = new FolderCompressorParameters(input, output, true, CompressionMode.NoOp, new ExecutionOptions(ThreadCount.Default), new ProcessingOptions(CompressionLevel.Default, CopyOtherFiles: true));

        Action prepare = () => compressor.PrepareNewRun(parameters, CancellationToken.None);

        prepare.Should().Throw<IOException>().WithMessage("*Linked folders*");
        fs.AllFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task LinkedDestinationSubfolderIsReportedWithoutWritingThroughIt()
    {
        var fs = new MockFileSystem();
        string input = NewPath("input");
        string output = NewPath("output");
        fs.AddFile(Path.Combine(input, "TIFF", "image.tif"), new MockFileData("pixels"));
        fs.AddFile(Path.Combine(input, "unrelated", "notes.txt"), new MockFileData("notes"));
        var linked = fs.Directory.CreateDirectory(Path.Combine(output, "TIFF"));
        linked.Attributes |= FileAttributes.ReparsePoint;

        var messages = await Run(fs, input, output);

        fs.File.Exists(Path.Combine(output, "TIFF", "image.tif")).Should().BeFalse();
        fs.File.ReadAllText(Path.Combine(output, "unrelated", "notes.txt")).Should().Be("notes");
        messages.Should().Contain(m => m.InputFile.Name == "image.tif" && m.ErrorMessage != null && m.ErrorMessage.Contains("Linked folders"));
    }

    [Fact]
    public void FailedCopyCannotRemoveAnExistingDestination()
    {
        string root = NewPath("fallback-collision");
        Directory.CreateDirectory(root);
        string input = Path.Combine(root, "input.czi");
        string output = Path.Combine(root, "output.czi");
        string temporary = Path.Combine(root, "our-owned-output");
        File.WriteAllText(input, "read-only source");
        File.SetAttributes(input, FileAttributes.ReadOnly);
        File.WriteAllText(output, "valuable existing destination");
        try
        {
            var fs = new FileSystem();
            string error = new FileProcessingFailedHandler().FileProcessingFailed(
                fs.FileInfo.New(input),
                fs.FileInfo.New(output),
                fs.FileInfo.New(temporary),
                _ => { },
                "Processing failed",
                true,
                CancellationToken.None);

            error.Should().Contain(FileProcessingFailedHandler.FailedCopyMessage);
            File.ReadAllText(output).Should().Be("valuable existing destination");
            File.ReadAllText(input).Should().Be("read-only source");
            File.Exists(temporary).Should().BeFalse();
        }
        finally
        {
            File.SetAttributes(input, FileAttributes.Normal);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RealDirectoryAliasCannotRecursivelyCopyOutputIntoItself()
    {
        string root = NewPath("alias-regression");
        string input = Path.Combine(root, "input");
        string target = Path.Combine(input, "output");
        string alias = Path.Combine(root, "alias");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "sentinel.txt"), "valuable sentinel");
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var start = new System.Diagnostics.ProcessStartInfo("cmd.exe")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                };
                foreach (string argument in new[] { "/c", "mklink", "/J", alias, target })
                {
                    start.ArgumentList.Add(argument);
                }

                using var process = System.Diagnostics.Process.Start(start)!;
                string diagnostic = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                process.ExitCode.Should().Be(0, diagnostic);
            }
            else
            {
                Directory.CreateSymbolicLink(alias, target);
            }

            Func<Task> run = () => Run(new FileSystem(), input, alias);
            await run.Should().ThrowAsync<IOException>().WithMessage("*Linked folders*");
            File.ReadAllText(Path.Combine(target, "sentinel.txt")).Should().Be("valuable sentinel");
            Directory.GetFileSystemEntries(target).Should().ContainSingle();
        }
        finally
        {
            if (Directory.Exists(alias))
            {
                Directory.Delete(alias, recursive: false);
            }

            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CopyOtherFiles_RealFilesAndNativeCompressionProduceCompleteDestination()
    {
        string root = NewPath("real-files");
        string input = Path.Combine(root, "input");
        string output = Path.Combine(root, "output");
        string tiff = Path.Combine(input, "TIFF", "image.tif");
        Directory.CreateDirectory(Path.GetDirectoryName(tiff)!);
        try
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, "mandelbrot.czi"), Path.Combine(input, "image.czi"));
            File.WriteAllText(Path.Combine(input, "unsupported.czi"), "unprocessable CZI");
            byte[] pixels = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
            File.WriteAllBytes(tiff, pixels);
            File.SetAttributes(tiff, FileAttributes.ReadOnly);
            Directory.CreateDirectory(Path.Combine(input, "empty"));
            var fs = new FileSystem();
            var compressor = new MultiThreadedFolderCompressor(PInvokeFileProcessor.Create, new FileProcessingFailedHandler());
            var parameters = new FolderCompressorParameters(
                fs.DirectoryInfo.New(input),
                fs.DirectoryInfo.New(output),
                true,
                CompressionMode.CompressUncompressed,
                new ExecutionOptions(new ThreadCount { Value = 1 }),
                new ProcessingOptions(CompressionLevel.Default, CopyFailedFiles: true, CopyOtherFiles: true));
            var run = compressor.PrepareNewRun(parameters, CancellationToken.None);
            List<CompressorMessage.FileFinished> messages = new();
            using var subscription = run.Output.OfType<CompressorMessage.FileFinished>().Subscribe(messages.Add);
            Task completion = run.Output.ToTask();

            run.Start();
            await completion;

            new FileInfo(Path.Combine(output, "image.czi")).Length.Should().Be(58528L);
            File.ReadAllBytes(Path.Combine(output, "TIFF", "image.tif")).Should().Equal(pixels);
            File.ReadAllText(Path.Combine(output, "unsupported.czi")).Should().Be("unprocessable CZI");
            Directory.Exists(Path.Combine(output, "empty")).Should().BeTrue();
            messages.Should().HaveCount(3).And.ContainSingle(m =>
                m.ErrorMessage != null && m.ErrorMessage.Contains(FileProcessingFailedHandler.SuccessfulCopyMessage));
        }
        finally
        {
            if (File.Exists(tiff))
            {
                File.SetAttributes(tiff, FileAttributes.Normal);
            }

            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task CopyOtherFiles_PreservesContentsFoldersAndReadOnlySources()
    {
        var fs = new MockFileSystem();
        string input = NewPath("input");
        string output = NewPath("output");
        fs.AddFile(Path.Combine(input, "image.CZI"), new MockFileData("original CZI"));
        byte[] pixels = Enumerable.Range(0, (1024 * 1024) + 9).Select(i => (byte)i).ToArray();
        string tiff = Path.Combine(input, "TIFF folder", "image.tiff");
        fs.AddFile(tiff, new MockFileData(pixels) { Attributes = FileAttributes.ReadOnly });
        fs.AddFile(Path.Combine(input, "notes.txt"), new MockFileData("experiment notes"));
        fs.AddFile(Path.Combine(input, "README"), new MockFileData(string.Empty));
        fs.Directory.CreateDirectory(Path.Combine(input, "empty folder"));
        List<string> compressedFiles = new();

        var messages = await Run(fs, input, output, compressedFiles);

        fs.File.ReadAllBytes(Path.Combine(output, "TIFF folder", "image.tiff")).Should().Equal(pixels);
        fs.File.ReadAllText(Path.Combine(output, "notes.txt")).Should().Be("experiment notes");
        fs.File.ReadAllText(Path.Combine(output, "README")).Should().BeEmpty();
        fs.Directory.Exists(Path.Combine(output, "empty folder")).Should().BeTrue();
        fs.File.ReadAllText(Path.Combine(output, "image.CZI")).Should().Be("compressed CZI");
        fs.File.ReadAllText(Path.Combine(input, "image.CZI")).Should().Be("original CZI");
        fs.File.GetLastWriteTimeUtc(Path.Combine(output, "TIFF folder", "image.tiff"))
            .Should().Be(fs.File.GetLastWriteTimeUtc(tiff));
        compressedFiles.Should().Equal(Path.Combine(input, "image.CZI"));
        messages.Should().HaveCount(4).And.OnlyContain(m => m.ErrorMessage == null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CopyOtherFiles_RespectsSubfolderSetting(bool recursive)
    {
        var fs = new MockFileSystem();
        string input = NewPath("input");
        string output = NewPath("output");
        fs.AddFile(Path.Combine(input, "notes.txt"), new MockFileData("root"));
        fs.AddFile(Path.Combine(input, "TIFF", "image.tif"), new MockFileData("pixels"));
        fs.Directory.CreateDirectory(Path.Combine(input, "empty"));

        await Run(fs, input, output, recursive: recursive);

        fs.File.Exists(Path.Combine(output, "notes.txt")).Should().BeTrue();
        fs.File.Exists(Path.Combine(output, "TIFF", "image.tif")).Should().Be(recursive);
        fs.Directory.Exists(Path.Combine(output, "empty")).Should().Be(recursive);
    }

    [Fact]
    public async Task CopyOtherFiles_WhenDisabled_ProcessesOnlyCzis()
    {
        var fs = new MockFileSystem();
        string input = NewPath("input");
        string output = NewPath("output");
        fs.AddFile(Path.Combine(input, "image.czi"), new MockFileData("original"));
        fs.AddFile(Path.Combine(input, "TIFF", "image.tiff"), new MockFileData("pixels"));
        fs.Directory.CreateDirectory(Path.Combine(input, "empty"));

        var messages = await Run(fs, input, output, copyOtherFiles: false);

        messages.Should().ContainSingle();
        fs.File.Exists(Path.Combine(output, "image.czi")).Should().BeTrue();
        fs.Directory.Exists(Path.Combine(output, "TIFF")).Should().BeFalse();
        fs.Directory.Exists(Path.Combine(output, "empty")).Should().BeFalse();
    }

    [Fact]
    public async Task CopyOtherFiles_DryRun_ListsFilesWithoutCreatingOutputs()
    {
        var fs = new MockFileSystem();
        string input = NewPath("input");
        string output = NewPath("output");
        fs.AddFile(Path.Combine(input, "image.czi"), new MockFileData("original"));
        fs.AddFile(Path.Combine(input, "TIFF", "image.tif"), new MockFileData("pixels"));
        fs.Directory.CreateDirectory(Path.Combine(input, "empty"));

        var messages = await Run(fs, input, output, mode: CompressionMode.NoOp);

        messages.Should().HaveCount(2).And.OnlyContain(m => m.ErrorMessage == null && m.SizeOutput == 0);
        fs.Directory.Exists(output).Should().BeFalse();
    }

    [Fact]
    public async Task CopyOtherFiles_ExistingDestinationIsReportedAndNotOverwritten()
    {
        var fs = new MockFileSystem();
        string input = NewPath("input");
        string output = NewPath("output");
        fs.AddFile(Path.Combine(input, "existing.txt"), new MockFileData("source"));
        fs.AddFile(Path.Combine(input, "new.txt"), new MockFileData("new"));
        fs.AddFile(Path.Combine(output, "existing.txt"), new MockFileData("keep me"));

        var messages = await Run(fs, input, output);

        fs.File.ReadAllText(Path.Combine(output, "existing.txt")).Should().Be("keep me");
        fs.File.ReadAllText(Path.Combine(output, "new.txt")).Should().Be("new");
        messages.Should().ContainSingle(m => m.ErrorMessage != null && m.ErrorMessage.Contains("already exists"));
    }

    [Fact]
    public async Task CopyOtherFiles_NestedDestinationIsExcluded()
    {
        var fs = new MockFileSystem();
        string input = NewPath("input");
        string output = Path.Combine(input, "output");
        fs.AddFile(Path.Combine(input, "image.czi"), new MockFileData("original"));
        fs.AddFile(Path.Combine(input, "notes.txt"), new MockFileData("notes"));
        fs.AddFile(Path.Combine(output, "old.czi"), new MockFileData("old output"));
        fs.AddFile(Path.Combine(output, "report.csv"), new MockFileData("old report"));
        fs.Directory.CreateDirectory(Path.Combine(output, "empty"));

        var messages = await Run(fs, input, output);

        messages.Should().HaveCount(2);
        fs.Directory.Exists(Path.Combine(output, "output")).Should().BeFalse();
        fs.File.ReadAllText(Path.Combine(output, "old.czi")).Should().Be("old output");
        fs.File.ReadAllText(Path.Combine(output, "notes.txt")).Should().Be("notes");
    }

    [Fact]
    public async Task CopyOtherFiles_CopyFailureIsReportedAndPartialOutputRemoved()
    {
        var fs = new FileSystem();
        string root = NewPath("locked-files");
        string input = Path.Combine(root, "input");
        string output = Path.Combine(root, "output");
        string file = Path.Combine(input, "locked.tif");
        Directory.CreateDirectory(input);
        try
        {
            File.WriteAllText(file, "pixels");
            using var locked = fs.FileStream.New(file, FileMode.Open, FileAccess.Read, FileShare.None);

            var messages = await Run(fs, input, output);

            messages.Should().ContainSingle(m => m.ErrorMessage != null);
            fs.File.Exists(Path.Combine(output, "locked.tif")).Should().BeFalse();
            fs.File.Exists(Path.Combine(output, "locked.tif~")).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void CopyFileProcessor_CancellationStopsCopy()
    {
        var fs = new MockFileSystem();
        string input = NewPath("input.tif");
        string output = NewPath("output.tif");
        byte[] pixels = new byte[3 * 1024 * 1024];
        fs.AddFile(input, new MockFileData(pixels));
        fs.Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using var cancellation = new CancellationTokenSource();
        using var processor = new CopyFileProcessor(fs);

        Action copy = () => processor.ProcessFile(input, output, _ => cancellation.Cancel(), cancellation.Token);

        copy.Should().Throw<OperationCanceledException>();
        fs.FileInfo.New(output).Length.Should().BeLessThan(pixels.Length);
    }

    [Fact]
    public void CopyOtherFiles_SameInputAndOutputIsRejected()
    {
        var fs = new MockFileSystem();
        var folder = fs.DirectoryInfo.New(NewPath("input"));
        var compressor = new MultiThreadedFolderCompressor((_, _) => new NoOpFileProcessor(), new FileProcessingFailedHandler());
        var parameters = new FolderCompressorParameters(
            folder,
            folder,
            true,
            CompressionMode.NoOp,
            new ExecutionOptions(ThreadCount.Default),
            new ProcessingOptions(CompressionLevel.Default, CopyOtherFiles: true));

        Action prepare = () => compressor.PrepareNewRun(parameters, CancellationToken.None);

        prepare.Should().Throw<ArgumentException>().WithMessage("*must be different*");
    }

    private static string NewPath(string name) => Path.Combine(
        Environment.GetEnvironmentVariable("CZISHRINK_TEST_DIRECTORY") ?? Path.GetTempPath(),
        "CziShrinkCopyTests",
        Guid.NewGuid().ToString(),
        name);

    private static async Task<List<CompressorMessage.FileFinished>> Run(
        IFileSystem fs,
        string input,
        string output,
        List<string>? compressedFiles = null,
        bool recursive = true,
        bool copyOtherFiles = true,
        CompressionMode mode = CompressionMode.CompressUncompressed)
    {
        var processor = new Mock<IFileProcessor>();
        processor.SetupGet(p => p.NeedsExistingOutputDirectory).Returns(true);
        processor.Setup(p => p.ProcessFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReportProgress>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, ReportProgress, CancellationToken>((source, destination, report, _) =>
            {
                compressedFiles?.Add(source);
                fs.File.WriteAllText(destination, "compressed CZI");
                report(100);
            });
        var compressor = new MultiThreadedFolderCompressor(
            (_, _) => mode == CompressionMode.NoOp ? new NoOpFileProcessor() : processor.Object,
            new FileProcessingFailedHandler())
        {
            MatchExtensionCasing = MatchCasing.PlatformDefault,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
            OtherFilesAttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        };
        var parameters = new FolderCompressorParameters(
            fs.DirectoryInfo.New(input),
            fs.DirectoryInfo.New(output),
            recursive,
            mode,
            new ExecutionOptions(new ThreadCount { Value = 1 }),
            new ProcessingOptions(CompressionLevel.Default, CopyOtherFiles: copyOtherFiles));
        var run = compressor.PrepareNewRun(parameters, CancellationToken.None);
        List<CompressorMessage.FileFinished> messages = new();
        using var subscription = run.Output.OfType<CompressorMessage.FileFinished>().Subscribe(messages.Add);
        Task completion = run.Output.ToTask();
        run.Start();
        await completion;
        return messages;
    }
}
