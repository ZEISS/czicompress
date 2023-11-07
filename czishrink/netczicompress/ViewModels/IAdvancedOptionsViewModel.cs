// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels;

using netczicompress.Models;

/// <summary>
///  A view model to define advanced options.
/// </summary>
public interface IAdvancedOptionsViewModel
{
    /// <summary>
    /// Gets or sets number of threads to use for operations.
    /// </summary>
    public ThreadCount ThreadCount { get; set; }

    /// <summary>
    /// Gets or sets the compression level to be used when compressing data.
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether failed files should be copied to the destination folder.
    /// </summary>
    public bool CopyFailedFiles { get; set; }
}
