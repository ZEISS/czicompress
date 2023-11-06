// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once

#include <string>

namespace utils::utf8
{
/// Widens UTF-8-encoded std::string into the platform-specific std::wstring.
/// This is 16-bit on Windows systems and 32-bit on UNIX systems.
/// It will throw an exception on invalid characters or if conversion failed.
///
/// \param          str    The UTF-8 encoded std::string.
///
/// \returns    The resulting std::wstring.
std::wstring WidenUtf8(const std::string& str);
}  // namespace utils::utf8
