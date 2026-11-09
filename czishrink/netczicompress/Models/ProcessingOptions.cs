// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

/// <summary>
/// Processing options to apply to an operation.
/// </summary>
/// <param name="CompressionLevel">Level of compression to use, if applicable.</param>
/// <param name="CopyFailedFiles">A value indicating whether failed files should be copied to the destination folder.</param>
/// <param name="CopyOtherFiles">Whether unselected files and empty folders should also be copied.</param>
public record ProcessingOptions(CompressionLevel CompressionLevel, bool CopyFailedFiles = false, bool CopyOtherFiles = false)
{
    public bool ProcessHdf5 { get; init; }

    public bool ProcessTiff { get; init; }

    public bool HasScientificFormats => this.ProcessHdf5 || this.ProcessTiff;

    public bool ProcessesExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".czi" => true,
        ".h5" or ".hdf5" => this.ProcessHdf5,
        ".tif" or ".tiff" => this.ProcessTiff,
        _ => false,
    };
}
