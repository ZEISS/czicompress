// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels;

using System.IO.Abstractions;
using System.Reactive;

using AutoFixture;

using netczicompress.Models;
using netczicompress.ViewModels;

using netczicompressTests.Models.Mocks;

/// <summary>
/// Tests for <see cref="MainViewModel"/>.
/// </summary>
/// <content>Tests for members related to mode selection.</content>
public partial class MainViewModelTests
{
    [Fact]
    public void Modes_WhenGot_HaveExpectedValues()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();

        var sut = fixture.Create<MainViewModel>();

        // ACT
        var actual = sut.Modes.Cast<OperationMode>().ToArray();

        // ASSERT
        actual.Should().BeEquivalentTo(new OperationMode[]
        {
            new(CompressionMode.CompressUncompressed, "Compress uncompressed data", "Compress only uncompressed subblocks and copy others."),
            new(CompressionMode.CompressUncompressedAndZstd, "Compress uncompressed and Zstd-compressed data", "Compress subblocks that were originally compressed with zstd or uncompressed."),
            new(CompressionMode.CompressAll, "Compress all data", "Compress all subblocks regardless of current compression method (if possible)."),
            new(CompressionMode.Decompress, "Decompress all data", "Decompress all possible subblocks."),
            new(CompressionMode.NoOp, "Dry Run (enumerate and log files)", "Do not compress/decompress rather just enumerate and log files that would be affected."),
        });
    }

    [Fact]
    public void SelectedMode_WhenGot_HasExpectedValue()
    {
        // ARRANGE
        IFixture fixture = CreateFixture();

        var sut = fixture.Create<MainViewModel>();

        // ACT
        var actual = sut.SelectedMode;

        // ASSERT
        actual.Should().BeSameAs(sut.Modes.Cast<OperationMode>().First());
        actual.Value.Should().Be(CompressionMode.CompressUncompressed);
    }

    [Theory]
    [AutoData]
    public async Task Start_WhenCalled_UsesSelectedMode(ushort selectedModeIndex)
    {
        IFixture fixture = CreateFixture();
        IFileSystem fs = NewMockFileSystem();
        fixture.Inject(fs);
        var compressorMock = fixture.Freeze<Mock<IFolderCompressor>>().WithAutoData(fixture);

        var sut = fixture.Create<MainViewModel>();
        SetFolders(sut, fs);

        var modes = sut.Modes.Cast<OperationMode>().ToArray();
        var selectedIndex = selectedModeIndex % modes.Length;
        var selectedMode = modes[selectedIndex];

        // ACT
        sut.SelectedMode = selectedMode;
        await sut.StartCommand.Execute(Unit.Default);

        // ASSERT
        compressorMock.Verify(
            x =>
                x.PrepareNewRun(
                    It.Is<FolderCompressorParameters>(x => x.Mode == selectedMode.Value),
                    It.Is<CancellationToken>(c => !c.IsCancellationRequested)),
            Times.Once);
    }

    [Theory]
    [AutoData]
    public void OperationMode_ToString_ReturnsDisplayText(CompressionMode mode, string displayText, string tooltipText)
    {
        var sut = new OperationMode(mode, displayText, tooltipText);

        sut.ToString().Should().BeSameAs(displayText);
    }

    private static IFixture CreateFixture(bool omitAutoProperties = true)
    {
        // ARRANGE
        return new Fixture { OmitAutoProperties = omitAutoProperties }.Customize(new AutoMoqCustomization());
    }
}
