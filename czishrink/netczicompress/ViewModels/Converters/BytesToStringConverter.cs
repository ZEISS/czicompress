// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels.Converters;

using System;
using System.Globalization;

/// <summary>
/// Converts an input value, assumed to be a byte count, to a compact string with an appropriate unit.
/// Expected input value types are <see cref="int"/> and <see cref="long"/>.
/// </summary>
public class BytesToStringConverter : ForwardOnlyConverter
{
    // See: https://en.wikipedia.org/wiki/Kilobyte
    // Made the array future-proof in any case.
    private static readonly string[] Sizes = { "bytes", "kB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB", "RB", "QB" };

    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            int intBytes => ConvertBytesToOrder(intBytes, culture),
            long longBytes => ConvertBytesToOrder(longBytes, culture),
            _ => 0,
        };
    }

    private static string ConvertBytesToOrder(long numBytes, CultureInfo culture)
    {
        double numBytesAsDouble = Math.Abs(numBytes);
        var order = 0;
        while (numBytesAsDouble >= 1000 && order < Sizes.Length - 1)
        {
            order++;
            numBytesAsDouble /= 1000;
        }

        var sign = numBytes < 0 ? "-" : string.Empty;
        var result = order == 0
            ? (FormattableString)$"{sign}{numBytesAsDouble:0.#} {Sizes[order]}"
            : (FormattableString)$"{sign}{numBytesAsDouble:0.00} {Sizes[order]}";
        return result.ToString(culture);
    }
}