// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.Reactive.Disposables;

/// <summary>
/// Tests for <see cref="PInvokeFileProcessor"/>.
/// </summary>
public class PInvokeFileProcessorTests
{
    [Fact]
    public void Ctr_WhenCalledWithNoOp_Throws()
    {
        Action act = () => _ = new PInvokeFileProcessor(CompressionMode.NoOp, new ProcessingOptions(new CompressionLevel()));

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("mode");
    }

    [Theory]
    [InlineData(CompressionMode.CompressUncompressed)]
    [InlineData(CompressionMode.CompressAll)]
    [InlineData(CompressionMode.CompressUncompressedAndZstd)]
    public void CompressAndDecompressOneFile_FilesHaveCorrectSize(
        CompressionMode mode)
    {
        // ARRANGE
        var testFile = GetTestFilePath();

        var compressed = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".czi");
        using var deleteCompressed = DeleteLater(compressed);

        var uncompressed = compressed + "-decompressed.czi";
        using var deleteUncompressed = Disposable.Create(() => File.Delete(uncompressed));

        // ACT
        using var compressor = new PInvokeFileProcessor(mode, new ProcessingOptions(new CompressionLevel()));
        compressor.ProcessFile(testFile, compressed, _ => { }, CancellationToken.None);

        using var decompressor = new PInvokeFileProcessor(CompressionMode.Decompress, new ProcessingOptions(new CompressionLevel()));
        decompressor.ProcessFile(compressed, uncompressed, _ => { }, CancellationToken.None);

        var compressedSize = GetLength(compressed);
        var decompressedSize = GetLength(uncompressed);
        var originalSize = GetLength(testFile);

        // ASSERT
        compressedSize.Should().Be(58432L);
        decompressedSize.Should().BeCloseTo(originalSize, 2048);
        decompressedSize.Should().Be(99552L);
    }

    [Theory]
    [InlineData(CompressionMode.CompressUncompressed)]
    [InlineData(CompressionMode.CompressAll)]
    [InlineData(CompressionMode.CompressUncompressedAndZstd)]
    [InlineData(CompressionMode.Decompress)]
    public void NeedsExistingOutputDirectory_ShouldBeTrue(
        CompressionMode mode)
    {
        using var target = new PInvokeFileProcessor(mode, new ProcessingOptions(new CompressionLevel()));
        target.NeedsExistingOutputDirectory.Should().BeTrue();
    }

    [Theory]
    [InlineData(CompressionMode.CompressUncompressed)]
    [InlineData(CompressionMode.CompressAll)]
    [InlineData(CompressionMode.CompressUncompressedAndZstd)]
    [InlineData(CompressionMode.Decompress)]
    public void ProcessFile_WhenCancelled_Throws(
        CompressionMode mode)
    {
        // ARRANGE
        string testFile = GetTestFilePath();

        var compressed = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".czi");
        using var deleteCompressed = DeleteLater(compressed);

        // ACT
        using var compressor = PInvokeFileProcessor.Create(mode, new ProcessingOptions(new CompressionLevel()));
        var token = new CancellationToken(true);
        Action act = () => compressor.ProcessFile(testFile, compressed, _ => { }, token);

        // ASSERT
        act.Should().Throw<OperationCanceledException>()
            .Which
            .CancellationToken.Should().Be(token);
    }

    [Theory]
    [InlineData(CompressionMode.CompressUncompressed)]
    [InlineData(CompressionMode.CompressAll)]
    [InlineData(CompressionMode.CompressUncompressedAndZstd)]
    [InlineData(CompressionMode.Decompress)]
    public void ProcessFile_WhenDisposed_Throws(
        CompressionMode mode)
    {
        // ARRANGE
        using var compressor = PInvokeFileProcessor.Create(mode, new ProcessingOptions(new CompressionLevel()));
        compressor.Dispose();

        Action act = () => compressor.ProcessFile("foo", "bar", _ => { }, CancellationToken.None);

        // ASSERT
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void GetLibFullName_WhenCalled_ReturnsExpected()
    {
        PInvokeFileProcessor.GetLibFullName().Should().Match("libczicompressc 0.*.*");
    }

    [Theory]
    [InlineData(0, 99552L, 57760L)]
    [InlineData(2, 99552L, 57920L)]
    [InlineData(4, 99552L, 57472L)]
    public void CompressWithLevel_FilesHaveCorrectSize(
        int compressionLevel, long expectedUncompressedSize, long expectedCompressedSize)
    {
        // ARRANGE
        var testFile = GetTestFilePath();

        var compressed = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".czi");
        using var deleteCompressed = DeleteLater(compressed);

        var uncompressed = compressed + "-decompressed.czi";
        using var deleteUncompressed = Disposable.Create(() => File.Delete(uncompressed));

        // ACT
        using var compressor = new PInvokeFileProcessor(
            CompressionMode.CompressAll,
            new ProcessingOptions(new CompressionLevel()
            {
                Value = compressionLevel,
            }));
        compressor.ProcessFile(testFile, compressed, _ => { }, CancellationToken.None);

        using var decompressor = new PInvokeFileProcessor(CompressionMode.Decompress, new ProcessingOptions(new CompressionLevel()));
        decompressor.ProcessFile(compressed, uncompressed, _ => { }, CancellationToken.None);

        var compressedSize = GetLength(compressed);
        var decompressedSize = GetLength(uncompressed);
        var originalSize = GetLength(testFile);

        // ASSERT
        compressedSize.Should().Be(expectedCompressedSize);
        decompressedSize.Should().Be(expectedUncompressedSize);
    }

    private static long GetLength(string path) => new FileInfo(path).Length;

    private static IDisposable DeleteLater(string path) => Disposable.Create(() => File.Delete(path));

    private static string GetTestFilePath()
    {
        var dirname = Path.GetDirectoryName(typeof(PInvokeFileProcessorTests).Assembly.Location);
        dirname.Should().NotBeNull();
        var testFile = Path.Combine(dirname ?? ".", "mandelbrot.czi");
        return testFile;
    }
}
