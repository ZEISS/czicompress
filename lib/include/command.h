// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once

/// Values that represent the commands - or describes the operation this program
/// should perform.
enum class Command
{
  kInvalid,     ///< An enum constant representing the invalid option
  kCompress,    ///< The source file should be compressed and written to the
                ///< destination file.
  kDecompress,  ///< The source file should be decompressed and written to the
                ///< destination file.
};
