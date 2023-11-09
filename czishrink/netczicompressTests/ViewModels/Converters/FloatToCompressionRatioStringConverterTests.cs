// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels.Converters;

using System.Globalization;
using netczicompress.ViewModels.Converters;

/// <summary>
/// Tests for <see cref="FloatToCompressionRatioStringConverter"/>.
/// </summary>
public class FloatToCompressionRatioStringConverterTests
{
    [Theory]
    [InlineData(0, "0.000")]
    [InlineData(1.23f, "1.230")]
    [InlineData(float.NaN, "")]
    public void CheckConversionWorksWithFloats(float value, string expected)
    {
        var converter = new FloatToCompressionRatioStringConverter();
        var result = converter.Convert(value, typeof(float), null, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "0.000")]
    [InlineData(1.23, "1.230")]
    [InlineData(double.NaN, "")]
    public void CheckConversionWorksWithDoubles(double value, string expected)
    {
        var converter = new FloatToCompressionRatioStringConverter();
        var result = converter.Convert(value, typeof(double), null, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("foo")]
    [InlineData(null)]
    [InlineData(typeof(string))]
    public void Convert_WhenCalledOnSomethingThatIsNeitherFloatNorDouble_ReturnsEmptyString(object value)
    {
        var converter = new FloatToCompressionRatioStringConverter();
        var result = converter.Convert(value, typeof(double), null, CultureInfo.InvariantCulture);
        result.Should().Be(string.Empty);
    }
}
