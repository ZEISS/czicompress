// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

/// <summary>
/// A view model for the selection of input and output folders.
/// </summary>
public interface IFolderInputOutputViewModel
{
    public string? InputDirectory { get; set; }

    public string? OutputDirectory { get; set; }
}