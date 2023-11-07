// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels.Converters;

using System;
using System.Globalization;

using Projektanker.Icons.Avalonia;

/// <summary>
/// Converts <see cref="AggregateIndicationStatus.Started"/> to <see cref="IconAnimation.Spin"/>,
/// and all other values to <see cref="IconAnimation.None"/>.
/// </summary>
public class AggregateStatusToAnimationConverter : ForwardOnlyConverter
{
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            AggregateIndicationStatus.Started => IconAnimation.Spin,
            _ => IconAnimation.None,
        };
    }
}
