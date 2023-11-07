// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

/// <summary>
/// Processing options to apply to an operation.
/// </summary>
/// <param name="CompressionLevel">Level of compression to use, if applicable.</param>
/// <param name="CopyFailedFiles">A value indicating whether failed files should be copied to the destination folder.</param>
public record ProcessingOptions(CompressionLevel CompressionLevel, bool CopyFailedFiles = false);
