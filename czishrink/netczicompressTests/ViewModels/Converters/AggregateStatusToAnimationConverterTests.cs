// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels.Converters;

using System.Globalization;

using netczicompress.ViewModels;
using netczicompress.ViewModels.Converters;

using Projektanker.Icons.Avalonia;

/// <summary>
/// Tests for <see cref="AggregateStatusToAnimationConverter"/>.
/// </summary>
public class AggregateStatusToAnimationConverterTests
{
    [Theory]
    [InlineData(AggregateIndicationStatus.Started, IconAnimation.Spin)]
    [InlineData(AggregateIndicationStatus.Cancelled, IconAnimation.None)]
    [InlineData(AggregateIndicationStatus.NotStarted, IconAnimation.None)]
    [InlineData(AggregateIndicationStatus.Error, IconAnimation.None)]
    [InlineData(AggregateIndicationStatus.Success, IconAnimation.None)]
    public void ConversionWorks(AggregateIndicationStatus status, IconAnimation expected)
    {
        var converter = new AggregateStatusToAnimationConverter();
        var result = converter.Convert(status, typeof(IconAnimation), null, CultureInfo.CurrentCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void BackConversion_Throws()
    {
        var converter = new AggregateStatusToAnimationConverter();
        Action act = () => converter.ConvertBack(null, typeof(void), null, CultureInfo.CurrentCulture);
        act.Should().Throw<System.NotSupportedException>();
    }
}