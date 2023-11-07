// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels.Converters;

using System.Globalization;
using netczicompress.ViewModels;
using netczicompress.ViewModels.Converters;
using Projektanker.Icons.Avalonia;

/// <summary>
/// Tests for <see cref="AggregateStatusToTextConverter"/>.
/// </summary>
public class AggregateStatusToTextConverterTests
{
    [Theory]
    [InlineData(AggregateIndicationStatus.NotStarted, "Compression not started")]
    [InlineData(AggregateIndicationStatus.Started, "Compression in progress")]
    [InlineData(AggregateIndicationStatus.Success, "Compression succeeded")]
    [InlineData(AggregateIndicationStatus.Error, "Compression had errors")]
    [InlineData(AggregateIndicationStatus.Cancelled, "Compression was cancelled")]
    public void ConversionWorks_ForStatusInput(AggregateIndicationStatus status, string expected)
    {
        var converter = new AggregateStatusToTextConverter();
        var result = converter.Convert(status, typeof(IconAnimation), null, CultureInfo.CurrentCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(true)]
    [InlineData("nonsense")]
    public void ConversionReturnsEmptyString_ForOtherInput(object input)
    {
        var converter = new AggregateStatusToTextConverter();
        var result = converter.Convert(input, typeof(IconAnimation), null, CultureInfo.CurrentCulture);
        result.Should().Be(string.Empty);
    }
}