// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels.Converters;

using System;
using System.Globalization;

/// <summary>
/// This converter is used to convert a floating point number (float or double) to a string
/// representation. The number format is fixed to 3 decimal places.
/// It is used for the "Output/Input Ratio" field, where the only specialty currently is that
/// the value NaN gives an empty string.
/// </summary>
public class FloatToCompressionRatioStringConverter : ForwardOnlyConverter
{
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            double doubleValue => ConvertToString(doubleValue, culture),
            float floatValue => ConvertToString(floatValue, culture),
            _ => string.Empty,
        };

        static string ConvertToString(double value, CultureInfo culture)
        {
            return value switch
            {
                double.NaN => string.Empty,
                _ => string.Format(culture, "{0:F3}", value),
            };
        }
    }
}
