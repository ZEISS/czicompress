// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels.Converters;

using System;
using System.Globalization;

/// <summary>
/// This converter delivers the name of an icon representing the aggregate status.
/// The input is expected to be of type <see cref="AggregateIndicationStatus"/>.
/// </summary>
public class AggregateStatusToIconConverter : ForwardOnlyConverter
{
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            AggregateIndicationStatus.Started => "fa-sync",
            _ => "fa-solid fa-circle-info",
        };
    }
}
