// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels.Converters;

using System.Globalization;
using netczicompress.ViewModels.Converters;
using Projektanker.Icons.Avalonia;

/// <summary>
/// Tests for <see cref="BytesToStringConverter"/>.
/// </summary>
public class BytesToStringConverterTests
{
    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(1000, "1.00 kB")]
    [InlineData(1234, "1.23 kB")]
    [InlineData(2345, "2.35 kB")]
    [InlineData(1e6, "1.00 MB")]
    [InlineData(1e9, "1.00 GB")]
    [InlineData(1e12, "1.00 TB")]
    [InlineData(1e15, "1.00 PB")]
    [InlineData(1e18, "1.00 EB")]
    public void ConversionWorksWithLong(long byteCount, string expected)
    {
        var converter = new BytesToStringConverter();
        var result = converter.Convert(byteCount, typeof(IconAnimation), null, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(1000, "1.00 kB")]
    [InlineData(1234, "1.23 kB")]
    [InlineData(2345, "2.35 kB")]
    [InlineData(1e6, "1.00 MB")]
    [InlineData(1e9, "1.00 GB")]
    public void ConversionWorksWithInt(int byteCount, string expected)
    {
        var converter = new BytesToStringConverter();
        var result = converter.Convert(byteCount, typeof(IconAnimation), null, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1e6, 0)]
    [InlineData(true, 0)]
    [InlineData(null, 0)]
    public void ConversionWithArbitraryInput_Returns0(object? byteCount, object expected)
    {
        var converter = new BytesToStringConverter();
        var result = converter.Convert(byteCount, typeof(IconAnimation), null, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }
}