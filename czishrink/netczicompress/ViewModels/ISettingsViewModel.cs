// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using System.Collections;

/// <summary>
/// A view model for settings to apply to operation.
/// </summary>
public interface ISettingsViewModel
{
    /// <summary>
    /// Gets a list of all possible output modes.
    /// </summary>
    public IEnumerable Modes { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the input directory shall be recursively traversed.
    /// </summary>
    public bool Recursive { get; set; }

    /// <summary>
    /// Gets or sets the selected Mode of operation.
    /// </summary>
    public OperationMode SelectedMode { get; set; }
}