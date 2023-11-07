// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;

/// <summary>
/// A view model for a list of ongoing compression tasks.
/// </summary>
public interface ICurrentTasksViewModel : INotifyPropertyChanged
{
    ReadOnlyObservableCollection<ICompressionTaskViewModel> CompressionTasks { get; }
}