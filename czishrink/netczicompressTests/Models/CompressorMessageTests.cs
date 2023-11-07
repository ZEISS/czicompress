// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.IO.Abstractions;

/// <summary>
/// Tests for <see cref="CompressorMessage"/> and its subclasses.
/// </summary>
public class CompressorMessageTests
{
    [Theory]
    [InlineAutoData(0)]
    public void SetZeroSizeInput_HasZeroSizeRatioAndDelta(long input, long output)
    {
        var fixture = new Fixture().Customize(new AutoMoqCustomization());
        var sut = new CompressorMessage.FileFinished(
            fixture.Create<IFileInfo>(),
            input,
            output,
            fixture.Create<TimeSpan?>(),
            null);

        sut.SizeDelta.Should().Be(output);
        sut.SizeRatio.Should().Be(0);
    }

    [Theory]
    [AutoData]
    public void CreateCompressionLevel_CalculatedPropertiesAreValid(long input, long output)
    {
        var fixture = new Fixture().Customize(new AutoMoqCustomization());
        var sut = new CompressorMessage.FileFinished(
            fixture.Create<IFileInfo>(),
            input,
            output,
            fixture.Create<TimeSpan?>(),
            null);

        sut.SizeInput.Should().Be(input);
        sut.SizeOutput.Should().Be(output);
        sut.SizeDelta.Should().Be(output - input);
        sut.SizeRatio.Should().Be(input == 0 ? 0 : (decimal)output / input);
    }
}