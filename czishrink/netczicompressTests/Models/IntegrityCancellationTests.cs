// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Reactive.Threading.Tasks;
using System.Security.Cryptography;

/// <summary>Tests cancellation during integrity checks and before output publication.</summary>
public class IntegrityCancellationTests
{
    [Fact]
    public void HashMatchesStandardDigestAndLeavesStreamOpen()
    {
        byte[] bytes = Enumerable.Range(0, 200000).Select(value => (byte)value).ToArray();
        using var stream = new MemoryStream(bytes);
        FileIntegrity.ComputeSha256(stream, CancellationToken.None).Should().Be(Convert.ToHexString(SHA256.HashData(bytes)));
        stream.CanRead.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationDuringReadIncludingEofNeverReturnsDigest(bool cancelAtEof)
    {
        using var cancellation = new CancellationTokenSource();
        using var stream = new CancelDuringReadStream(cancellation, cancelAtEof);
        Action hash = () => FileIntegrity.ComputeSha256(stream, cancellation.Token);
        hash.Should().Throw<OperationCanceledException>();
        stream.LargestRead.Should().BeLessThanOrEqualTo(64 * 1024);
    }

    [Fact]
    public void FallbackCancellationNeverPublishesOutput()
    {
        string root = Path.Combine(Environment.GetEnvironmentVariable("CZISHRINK_TEST_DIRECTORY") ?? Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var fs = new FileSystem();
            string input = Path.Combine(root, "input.h5");
            string output = Path.Combine(root, "output.h5");
            string temporary = Path.Combine(root, "temporary.h5");
            File.WriteAllText(input, "valuable original");
            using var cancellation = new CancellationTokenSource();
            var handler = new FileProcessingFailedHandler();
            Action copy = () => handler.FileProcessingFailed(
                fs.FileInfo.New(input),
                fs.FileInfo.New(output),
                fs.FileInfo.New(temporary),
                progress => cancellation.Cancel(),
                "test failure",
                true,
                cancellation.Token);

            copy.Should().Throw<OperationCanceledException>();
            File.Exists(output).Should().BeFalse();
            File.Exists(temporary).Should().BeFalse();
            File.ReadAllText(input).Should().Be("valuable original");
            handler.LastDetails.Should().BeNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task CancellationAfterProcessorReturnsNeverPublishesOutput()
    {
        var fs = new MockFileSystem();
        string input = fs.Path.GetFullPath("input");
        string output = fs.Path.GetFullPath("output");
        fs.AddFile(fs.Path.Combine(input, "sample.czi"), new MockFileData("valuable original"));
        fs.Directory.CreateDirectory(output);
        using var cancellation = new CancellationTokenSource();
        var processor = new Mock<IFileProcessor>();
        processor.SetupGet(p => p.NeedsExistingOutputDirectory).Returns(true);
        processor.Setup(p => p.ProcessFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReportProgress>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, ReportProgress, CancellationToken>((_, destination, _, _) =>
            {
                fs.File.WriteAllText(destination, "processed");
                cancellation.Cancel();
            });
        var compressor = new MultiThreadedFolderCompressor((_, _) => processor.Object, new FileProcessingFailedHandler())
        {
            MatchExtensionCasing = MatchCasing.PlatformDefault,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        };
        var parameters = new FolderCompressorParameters(
            fs.DirectoryInfo.New(input),
            fs.DirectoryInfo.New(output),
            true,
            CompressionMode.CompressAll,
            new ExecutionOptions(new ThreadCount { Value = 1 }),
            new ProcessingOptions(CompressionLevel.Default));
        var run = compressor.PrepareNewRun(parameters, cancellation.Token);
        Task completion = run.Output.ToTask();
        run.Start();
        Func<Task> wait = () => completion;
        await wait.Should().ThrowAsync<OperationCanceledException>();
        fs.Directory.GetFiles(output, "*", SearchOption.AllDirectories).Should().BeEmpty();
        fs.File.ReadAllText(fs.Path.Combine(input, "sample.czi")).Should().Be("valuable original");
    }

    private sealed class CancelDuringReadStream(CancellationTokenSource cancellation, bool cancelAtEof)
        : MemoryStream(new byte[130000])
    {
        public int LargestRead { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            this.LargestRead = Math.Max(this.LargestRead, count);
            int read = base.Read(buffer, offset, count);
            if ((read == 0) == cancelAtEof)
            {
                cancellation.Cancel();
            }

            return read;
        }
    }
}
