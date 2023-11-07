// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels.Converters;

using System.Globalization;

using Avalonia.Media;

using netczicompress.ViewModels;
using netczicompress.ViewModels.Converters;

using Projektanker.Icons.Avalonia;

/// <summary>
/// Tests for <see cref="AggregateStatusToForegroundConverter"/>.
/// </summary>
public class AggregateStatusToForegroundConverterTests
{
    public static IEnumerable<object[]> DataWithStatusInput =>
        new List<object[]>
        {
            new object[] { AggregateIndicationStatus.NotStarted, Colors.Black },
            new object[] { AggregateIndicationStatus.Started, Colors.CadetBlue },
            new object[] { AggregateIndicationStatus.Success, Colors.Green },
            new object[] { AggregateIndicationStatus.Error, Colors.Red },
            new object[] { AggregateIndicationStatus.Cancelled, Colors.LightGray },
        };

    public static IEnumerable<object[]> DataWithOtherInput =>
        new List<object[]>
        {
            new object[] { 999, Colors.Red },
            new object[] { true, Colors.Red },
            new object[] { new(), Colors.Red },
        };

    [Theory]
    [MemberData(nameof(DataWithStatusInput))]
    public void ForStatusInput_ConversionWorks(AggregateIndicationStatus status, Color expected)
    {
        var converter = new AggregateStatusToForegroundConverter();
        var result = converter.Convert(status, typeof(IconAnimation), null, CultureInfo.CurrentCulture);
        IImmutableSolidColorBrush? brush = result as IImmutableSolidColorBrush;
        brush.Should().NotBeNull();
        brush?.Color.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(DataWithOtherInput))]
    public void ForArbitraryInput_ConversionWorks(object status, Color expected)
    {
        var converter = new AggregateStatusToForegroundConverter();
        var result = converter.Convert(status, typeof(IconAnimation), null, CultureInfo.CurrentCulture);
        IImmutableSolidColorBrush? brush = result as IImmutableSolidColorBrush;
        brush.Should().NotBeNull();
        brush?.Color.Should().Be(expected);
    }
}
