// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once

/// Values that represent the compression strategies - or, in other words, which
/// subblocks to consider for compression.
enum class CompressionStrategy
{
  kInvalid,                        ///< An enum constant representing the invalid option
  kAll,                            ///< All subblocks are considered for compression, irrespective of
                                   ///< their current compression state.
  kOnlyUncompressed,               ///< Only subblocks that are currently uncompressed are
                                   ///< considered for compression.
  kUncompressedAndZStdCompressed,  ///< Only subblocks that are currently
                                   ///< uncompressed or ZStd compressed are
                                   ///< considered for compression.
};
