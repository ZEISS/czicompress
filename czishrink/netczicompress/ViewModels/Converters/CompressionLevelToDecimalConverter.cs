// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels.Converters;

using System.Globalization;
using Avalonia.Data.Converters;
using netczicompress.Models;

public class CompressionLevelToDecimalConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            CompressionLevel compressionLevel => (decimal)compressionLevel.Value,
            _ => (decimal)CompressionLevel.DefaultValue,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            decimal decimalValue => new CompressionLevel { Value = (int)decimalValue },
            _ => CompressionLevel.Default,
        };
    }
}