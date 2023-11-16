// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.Reactive.Disposables;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;

/// <summary>
/// Tests for <see cref="PInvokeFileProcessor"/>.
/// </summary>
public class PInvokeFileProcessorTests
{
    [Fact]
    public void Ctr_WhenCalledWithNoOp_Throws()
    {
        Action act = () => _ = new PInvokeFileProcessor(CompressionMode.NoOp, new ProcessingOptions(new CompressionLevel { Value = CompressionLevel.DefaultValue }));

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("mode");
    }

    [Theory]
    [InlineData(CompressionMode.CompressUncompressed)]
    [InlineData(CompressionMode.CompressAll)]
    [InlineData(CompressionMode.CompressUncompressedAndZstd)]
    public void CompressAndDecompressOneFile_FilesHaveCorrectSizeAndMetadata(
        CompressionMode mode)
    {
        // ARRANGE
        var testFile = GetTestFilePath();

        var compressed = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".czi");
        using var deleteCompressed = DeleteLater(compressed);

        var uncompressed = compressed + "-decompressed.czi";
        using var deleteUncompressed = Disposable.Create(() => File.Delete(uncompressed));

        // ACT
        using var compressor = new PInvokeFileProcessor(mode, new ProcessingOptions(CompressionLevel.Default));
        compressor.ProcessFile(testFile, compressed, _ => { }, CancellationToken.None);

        using var decompressor = new PInvokeFileProcessor(CompressionMode.Decompress, new ProcessingOptions(CompressionLevel.Default));
        decompressor.ProcessFile(compressed, uncompressed, _ => { }, CancellationToken.None);

        var compressedSize = GetLength(compressed);

        var decompressedSize = GetLength(uncompressed);
        var originalSize = GetLength(testFile);

        // ASSERT
        compressedSize.Should().Be(58528L);
        decompressedSize.Should().BeCloseTo(originalSize, 2048);
        decompressedSize.Should().Be(99648L);

        Metadata.FromFile(compressed).CurrentCompressionParameters.Should().Be("Lossless: True");
        Metadata.FromFile(uncompressed).CurrentCompressionParameters.Should().BeEmpty();
    }

    [Theory]
    [InlineData(CompressionMode.CompressUncompressed)]
    [InlineData(CompressionMode.CompressAll)]
    [InlineData(CompressionMode.CompressUncompressedAndZstd)]
    [InlineData(CompressionMode.Decompress)]
    public void NeedsExistingOutputDirectory_ShouldBeTrue(
        CompressionMode mode)
    {
        using var target = new PInvokeFileProcessor(mode, new ProcessingOptions(CompressionLevel.Default));
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
        using var compressor = PInvokeFileProcessor.Create(mode, new ProcessingOptions(CompressionLevel.Default));
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
        using var compressor = PInvokeFileProcessor.Create(mode, new ProcessingOptions(CompressionLevel.Default));
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
    [InlineData(0, 99648L, 57856L)]
    [InlineData(2, 99648L, 58016L)]
    [InlineData(4, 99648L, 57568L)]
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
            new ProcessingOptions(new CompressionLevel
            {
                Value = compressionLevel,
            }));
        compressor.ProcessFile(testFile, compressed, _ => { }, CancellationToken.None);

        using var decompressor = new PInvokeFileProcessor(CompressionMode.Decompress, new ProcessingOptions(CompressionLevel.Default));
        decompressor.ProcessFile(compressed, uncompressed, _ => { }, CancellationToken.None);

        var compressedSize = GetLength(compressed);
        var decompressedSize = GetLength(uncompressed);

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

    private class Metadata
    {
        public required XDocument Xml { get; init; }

        public string? CurrentCompressionParameters => this.Xml
            .XPathSelectElement(
                "ImageDocument/Metadata/Information/Image/CurrentCompressionParameters")
            ?.Value;

        public static Metadata FromFile(string path) => new() { Xml = ParseMetadataXml(path) };

        private static XDocument ParseMetadataXml(string filePath)
        {
            return XDocument.Parse(ReadMetadataXml(filePath));
        }

        private static string ReadMetadataXml(string filePath)
        {
            // These numbers are from the ZISRAW file specification.
            const int SegmentHeaderSize = 16 + 8 + 8;
            const int MetadataPositionInFileHeader = 60;
            const int MetadataHeaderSize = 256;

            using var stream = File.OpenRead(filePath);

            // BinaryReader uses little-endian as required.
            using var reader = new BinaryReader(stream);

            // Read metadataPosition from ZISRAW file header
            stream.Position = SegmentHeaderSize + MetadataPositionInFileHeader;
            long metadataHeaderPosition = reader.ReadInt64() + SegmentHeaderSize;

            // Read size of XML from ZISRAWMETADATA header
            stream.Position = metadataHeaderPosition;
            int metadataSize = reader.ReadInt32();

            // Read XML bytes from ZISRAWMETADATA segment
            stream.Position = metadataHeaderPosition + MetadataHeaderSize;
            var xmlBytes = reader.ReadBytes(metadataSize);

            // Decode (always UTF-8 according to the spec)
            var result = Encoding.UTF8.GetString(xmlBytes);
            return result;
        }
    }
}
