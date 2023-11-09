// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels.Converters;

using System.Globalization;
using Avalonia.Data.Converters;
using netczicompress.Models;

public class ThreadCountToDecimalConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            ThreadCount threadCount => (decimal)threadCount.Value,
            _ => (decimal)ThreadCount.Default.Value,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            decimal decimalValue => new ThreadCount { Value = (int)decimalValue },
            _ => ThreadCount.Default,
        };
    }
}