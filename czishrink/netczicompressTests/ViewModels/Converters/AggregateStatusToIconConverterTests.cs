// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels.Converters;

using System.Globalization;
using netczicompress.ViewModels;
using netczicompress.ViewModels.Converters;
using Projektanker.Icons.Avalonia;

/// <summary>
/// Tests for <see cref="AggregateStatusToIconConverter"/>.
/// </summary>
public class AggregateStatusToIconConverterTests
{
    [Theory]
    [InlineData(AggregateIndicationStatus.Started, "fa-sync")]
    [InlineData(AggregateIndicationStatus.Cancelled, "fa-solid fa-circle-info")]
    [InlineData(AggregateIndicationStatus.NotStarted, "fa-solid fa-circle-info")]
    [InlineData(AggregateIndicationStatus.Error, "fa-solid fa-circle-info")]
    [InlineData(AggregateIndicationStatus.Success, "fa-solid fa-circle-info")]
    public void ConversionWorks(AggregateIndicationStatus status, string expected)
    {
        var converter = new AggregateStatusToIconConverter();
        var result = converter.Convert(status, typeof(IconAnimation), null, CultureInfo.CurrentCulture);
        result.Should().Be(expected);
    }
}