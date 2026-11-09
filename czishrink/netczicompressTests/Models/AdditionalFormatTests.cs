// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Threading.Tasks;

/// <summary>Tests opt-in native-format processing and source/destination protection.</summary>
public class AdditionalFormatTests
{
    [Theory]
    [InlineData(".czi", false, false, true)]
    [InlineData(".H5", true, false, true)]
    [InlineData(".hdf5", true, false, true)]
    [InlineData(".h5", false, true, false)]
    [InlineData(".TIFF", false, true, true)]
    [InlineData(".tif", false, true, true)]
    [InlineData(".tif", true, false, false)]
    [InlineData(".csv", true, true, false)]
    public void OnlySelectedFormatsAreProcessed(string extension, bool hdf5, bool tiff, bool expected)
    {
        new ProcessingOptions(CompressionLevel.Default) { ProcessHdf5 = hdf5, ProcessTiff = tiff }
            .ProcessesExtension(extension).Should().Be(expected);
    }

    [Theory]
    [InlineData(false, false, 1)]
    [InlineData(true, false, 2)]
    [InlineData(false, true, 2)]
    [InlineData(true, true, 3)]
    public async Task FolderRunSelectsFormatsAndPreservesUnselectedFiles(bool hdf5, bool tiff, int expected)
    {
        var fs = new MockFileSystem();
        string input = fs.Path.GetFullPath("input");
        string output = fs.Path.GetFullPath("output");
        foreach (string name in new[] { "image.czi", "recording.h5", "image.tiff", "notes.txt" })
        {
            fs.AddFile(fs.Path.Combine(input, name), new MockFileData("read-only source") { Attributes = FileAttributes.ReadOnly });
        }

        var messages = await Run(fs, input, output, hdf5, tiff);

        messages.Should().HaveCount(expected).And.OnlyContain(m => m.ErrorMessage == null);
        fs.Directory.GetFiles(output).Should().HaveCount(expected);
        fs.File.Exists(fs.Path.Combine(output, "notes.txt")).Should().BeFalse();
        foreach (string source in fs.Directory.GetFiles(input))
        {
            fs.File.ReadAllText(source).Should().Be("read-only source");
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public async Task CopyOtherFilesCopiesOnlyUnselectedFormatsAndDryRunCreatesNothing(bool hdf5, bool tiff, bool dryRun)
    {
        var fs = new MockFileSystem();
        string input = fs.Path.GetFullPath("input");
        string output = fs.Path.GetFullPath("output");
        var names = new[] { "image.czi", "recording.h5", "image.tiff", "notes.txt" };
        foreach (string name in names)
        {
            fs.AddFile(fs.Path.Combine(input, name), new MockFileData("original " + name) { Attributes = FileAttributes.ReadOnly });
        }

        fs.Directory.CreateDirectory(fs.Path.Combine(input, "empty"));
        var mode = dryRun ? CompressionMode.NoOp : CompressionMode.CompressAll;
        var messages = await Run(fs, input, output, hdf5, tiff, mode, copyOtherFiles: true);

        messages.Should().HaveCount(4).And.OnlyContain(message => message.ErrorMessage == null);
        if (dryRun)
        {
            fs.Directory.Exists(output).Should().BeFalse();
        }
        else
        {
            fs.Directory.Exists(fs.Path.Combine(output, "empty")).Should().BeTrue();
            foreach (string name in names)
            {
                bool selected = name.EndsWith(".czi", StringComparison.Ordinal)
                    || (hdf5 && name.EndsWith(".h5", StringComparison.Ordinal))
                    || (tiff && name.EndsWith(".tiff", StringComparison.Ordinal));
                fs.File.ReadAllText(fs.Path.Combine(output, name)).Should().Be(selected ? "processed" : "original " + name);
                if (!selected)
                {
                    var message = messages.Single(value => value.InputFile.Name == name);
                    message.Details!.ContainerByteIdentical.Should().BeTrue();
                    message.Details.InputSha256.Should().Be(message.Details.OutputSha256);
                }
            }
        }

        foreach (string name in names)
        {
            fs.File.ReadAllText(fs.Path.Combine(input, name)).Should().Be("original " + name);
            fs.File.GetAttributes(fs.Path.Combine(input, name)).Should().HaveFlag(FileAttributes.ReadOnly);
        }
    }

    [Fact]
    public async Task NestedOutputAndBlockedFolderDoNotPreventSiblingProcessing()
    {
        var fs = new MockFileSystem();
        string input = fs.Path.GetFullPath("input");
        string output = fs.Path.Combine(input, "output");
        fs.AddFile(fs.Path.Combine(input, "blocked", "image.tif"), new MockFileData("pixels"));
        fs.AddFile(fs.Path.Combine(input, "sibling", "recording.h5"), new MockFileData("data"));
        fs.AddFile(fs.Path.Combine(output, "blocked"), new MockFileData("valuable blocker"));
        fs.AddFile(fs.Path.Combine(output, "old.h5"), new MockFileData("valuable prior output"));

        var messages = await Run(fs, input, output, true, true);

        messages.Should().HaveCount(2).And.ContainSingle(m => m.ErrorMessage != null);
        fs.File.ReadAllText(fs.Path.Combine(output, "blocked")).Should().Be("valuable blocker");
        fs.File.ReadAllText(fs.Path.Combine(output, "old.h5")).Should().Be("valuable prior output");
        fs.File.ReadAllText(fs.Path.Combine(output, "sibling", "recording.h5")).Should().Be("processed");
        fs.Directory.Exists(fs.Path.Combine(output, "output")).Should().BeFalse();
    }

    [Fact]
    public async Task DryRunPreservesExistingTildeNamedFiles()
    {
        var fs = new MockFileSystem();
        string input = fs.Path.GetFullPath("input");
        string output = fs.Path.GetFullPath("output");
        fs.AddFile(fs.Path.Combine(input, "recording.h5"), new MockFileData("original"));
        fs.AddFile(fs.Path.Combine(output, "recording.h5~"), new MockFileData("valuable tilde file"));

        var messages = await Run(fs, input, output, true, true, CompressionMode.NoOp);

        messages.Should().ContainSingle(m => m.ErrorMessage == null && m.SizeOutput == 0);
        fs.File.ReadAllText(fs.Path.Combine(output, "recording.h5~")).Should().Be("valuable tilde file");
        fs.Directory.GetFiles(output).Should().ContainSingle();
        fs.Directory.GetDirectories(output).Should().BeEmpty();
    }

    [Fact]
    public async Task ReadOnlyFallbackReportsTheActualCopiedOutputSize()
    {
        string root = Path.Join(Environment.GetEnvironmentVariable("CZISHRINK_TEST_DIRECTORY") ?? Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string input = Path.Join(root, "input");
        string output = Path.Join(root, "output");
        Directory.CreateDirectory(input);
        string source = Path.Join(input, "recording.h5");
        File.WriteAllText(source, "original recording bytes");
        File.SetAttributes(source, FileAttributes.ReadOnly);
        try
        {
            var fs = new FileSystem();
            var processor = new Mock<IFileProcessor>();
            processor.SetupGet(p => p.NeedsExistingOutputDirectory).Returns(true);
            processor.Setup(p => p.ProcessFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReportProgress>(), It.IsAny<CancellationToken>()))
                .Throws(new IOException("Unsupported test layout"));
            var compressor = new MultiThreadedFolderCompressor((_, _) => processor.Object, new FileProcessingFailedHandler());
            var parameters = new FolderCompressorParameters(
                fs.DirectoryInfo.New(input),
                fs.DirectoryInfo.New(output),
                true,
                CompressionMode.CompressAll,
                new ExecutionOptions(new ThreadCount { Value = 1 }),
                new ProcessingOptions(CompressionLevel.Default, CopyFailedFiles: true) { ProcessHdf5 = true });
            var run = compressor.PrepareNewRun(parameters, CancellationToken.None);
            List<CompressorMessage.FileFinished> results = new();
            using var subscription = run.Output.OfType<CompressorMessage.FileFinished>().Subscribe(results.Add);
            Task completion = run.Output.ToTask();
            run.Start();
            await completion;

            results.Should().ContainSingle();
            results[0].ErrorMessage.Should().Contain(FileProcessingFailedHandler.SuccessfulCopyMessage);
            results[0].SizeOutput.Should().Be(new FileInfo(source).Length);
            results[0].Details!.ContainerByteIdentical.Should().BeTrue();
            File.ReadAllText(Path.Join(output, "recording.h5")).Should().Be("original recording bytes");
            File.GetAttributes(source).Should().HaveFlag(FileAttributes.ReadOnly);
        }
        finally
        {
            File.SetAttributes(source, FileAttributes.Normal);
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AdditionalFormatsRejectInPlaceWork(bool hdf5, bool tiff)
    {
        var fs = new MockFileSystem();
        var folder = fs.Directory.CreateDirectory(fs.Path.GetFullPath("input"));
        var compressor = new MultiThreadedFolderCompressor((_, _) => new NoOpFileProcessor(), new FileProcessingFailedHandler());
        var parameters = new FolderCompressorParameters(
            folder,
            folder,
            true,
            CompressionMode.NoOp,
            new ExecutionOptions(ThreadCount.Default),
            new ProcessingOptions(CompressionLevel.Default) { ProcessHdf5 = hdf5, ProcessTiff = tiff });
        Action prepare = () => compressor.PrepareNewRun(parameters, CancellationToken.None);
        prepare.Should().Throw<ArgumentException>().WithMessage("*must be different*");
    }

    [Fact]
    public void MissingRuntimeIsReportedWithoutCreatingOutput()
    {
        using var worker = new ScientificFileProcessor(CompressionMode.CompressAll, CompressionLevel.Default, Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Action process = () => worker.ProcessFile("absent.h5", "absent-output.h5", _ => { }, CancellationToken.None);
        process.Should().Throw<IOException>().WithMessage("*tools are missing*");
        worker.LastDetails.Should().BeNull();
    }

    private static async Task<List<CompressorMessage.FileFinished>> Run(
        IFileSystem fs, string input, string output, bool hdf5, bool tiff, CompressionMode mode = CompressionMode.CompressAll, bool copyOtherFiles = false)
    {
        var processor = new Mock<IFileProcessor>();
        processor.SetupGet(p => p.NeedsExistingOutputDirectory).Returns(true);
        processor.Setup(p => p.ProcessFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReportProgress>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, ReportProgress, CancellationToken>((_, destination, progress, _) =>
            {
                fs.File.WriteAllText(destination, "processed");
                progress(100);
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
            true,
            mode,
            new ExecutionOptions(new ThreadCount { Value = copyOtherFiles ? 2 : 1 }),
            new ProcessingOptions(CompressionLevel.Default, CopyOtherFiles: copyOtherFiles) { ProcessHdf5 = hdf5, ProcessTiff = tiff });
        var run = compressor.PrepareNewRun(parameters, CancellationToken.None);
        List<CompressorMessage.FileFinished> messages = new();
        using var subscription = run.Output.OfType<CompressorMessage.FileFinished>().Subscribe(messages.Add);
        Task completion = run.Output.ToTask();
        run.Start();
        await completion;
        return messages;
    }
}
