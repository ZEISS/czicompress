// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

/// <summary>
/// A service that launches a file with its associated app.
/// </summary>
public interface IFileLauncher
{
    /// <summary>
    /// Launches the specified file or folder with its associated app.
    /// </summary>
    /// <param name="path">The file or folder to launch.</param>
    /// <returns>A task that represents the operation.</returns>
    Task Launch(string path);

    /// <summary>
    /// Shows the specified file or folder in the operating system's file manager.
    /// </summary>
    /// <param name="path">The file or folder to show.</param>
    /// <returns>A task that represents the operation.</returns>
    Task Reveal(string path);
}