// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels.Converters;

using System;
using System.Globalization;

using Avalonia.Media;

/// <summary>
/// This converter delivers the color for an icon
/// representing the aggregate status.
/// The input is expected to be of type <see cref="AggregateIndicationStatus"/>.
/// </summary>
public class AggregateStatusToForegroundConverter : ForwardOnlyConverter
{
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            AggregateIndicationStatus.NotStarted => Brushes.Black,
            AggregateIndicationStatus.Started => Brushes.CadetBlue,
            AggregateIndicationStatus.Success => Brushes.Green,
            AggregateIndicationStatus.Error => Brushes.Red,
            AggregateIndicationStatus.Cancelled => Brushes.LightGray,
            _ => Brushes.Red,
        };
    }
}
