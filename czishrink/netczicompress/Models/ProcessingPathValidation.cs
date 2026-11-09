// SPDX-FileCopyrightText: 2026 czicompress contributors
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.IO.Abstractions;

/// <summary>Checks folder paths before any output directories or files are created.</summary>
internal static class ProcessingPathValidation
{
    /// <summary>Rejects a folder whose path traverses a directory link.</summary>
    /// <param name="directory">The folder path to check, including existing ancestors.</param>
    public static void RejectLinkedDirectories(IDirectoryInfo directory)
    {
        for (IDirectoryInfo? current = directory; current != null; current = current.Parent)
        {
            current.Refresh();
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"Linked folders and linked ancestors are not supported: {current.FullName}");
            }
        }
    }
}
