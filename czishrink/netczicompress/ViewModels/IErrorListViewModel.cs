// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

/// <summary>
/// A view model for all errors of a folder compression operation.
/// </summary>
public interface IErrorListViewModel : INotifyPropertyChanged
{
    ReadOnlyObservableCollection<ErrorItem> Errors { get; }

    ErrorItem? SelectedErrorItem { get; set; }

    ICommand ShowSelectedFileCommand { get; }

    ICommand OpenSelectedFileCommand { get; }
}