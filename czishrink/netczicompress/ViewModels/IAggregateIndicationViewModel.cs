// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using netczicompress.Models;

/// <summary>
/// A view model that wraps a sequence of <see cref="AggregateStatistics"/>.
/// </summary>
public interface IAggregateIndicationViewModel
{
    AggregateIndicationStatus IndicationStatus { get; }
}