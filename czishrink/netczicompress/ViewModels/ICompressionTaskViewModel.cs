// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System.ComponentModel;

/// <summary>
/// A view model for an ongoing compression task.
/// </summary>
public interface ICompressionTaskViewModel : INotifyPropertyChanged
{
    string FileName { get; }

    int ProgressPercent { get; }

    bool IsIndeterminateProgress { get; }
}