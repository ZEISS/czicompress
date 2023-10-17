// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once

#include <CZICompress_Config.h>

#if CZICOMPRESS_WIN32_ENVIRONMENT
#include <codecvt>
#include <string>
#include <vector>

/// A utility which is providing the command-line arguments (on Windows) as
/// UTF8-encoded strings.
class CommandlineArgsWindowsHelper
{
private:
  std::vector<std::string> arguments_;
  std::vector<char*> pointers_to_arguments_;

public:
  /// Constructor.
  CommandlineArgsWindowsHelper();

  /// Gets an array of pointers to null-terminated, UTF8-encoded arguments. This
  /// size of this array is given by the "GetArgc"-method. Note that this
  /// pointer is only valid for the lifetime of this instance of the
  /// CommandlineArgsWindowsHelper-class.
  ///
  /// \returns    Pointer to an array of pointers to null-terminated,
  /// UTF8-encoded arguments.
  const char* const* GetArgv() const;

  /// Gets the number of arguments.
  ///
  /// \returns    The number of arguments.
  int GetArgc() const;
};
#endif  // CZICOMPRESS_WIN32_ENVIRONMENT
