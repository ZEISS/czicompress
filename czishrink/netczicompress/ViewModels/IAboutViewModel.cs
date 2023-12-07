// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System.Windows.Input;

/// <summary>
/// A view model that displays version and license information.
/// </summary>
public interface IAboutViewModel
{
    ICommand CloseAboutCommand { get; }

    bool IsVisible { get; set; }

    string LibraryName { get; }

    ICommand ShowTextFileCommand { get; }

    ICommand OpenUrlCommand { get; }
}