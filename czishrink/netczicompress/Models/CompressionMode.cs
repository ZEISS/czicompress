// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

/// <summary>
/// The compression modes supported by the application.
/// </summary>
public enum CompressionMode
{
    CompressUncompressed,
    CompressAll,
    CompressUncompressedAndZstd,
    Decompress,
    NoOp,
}
