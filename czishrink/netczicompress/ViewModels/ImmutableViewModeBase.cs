// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System.ComponentModel;

/// <summary>
/// Base class for immutable view models.
/// </summary>
public class ImmutableViewModeBase : INotifyPropertyChanged
{
    event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
    {
        add { }
        remove { }
    }
}
