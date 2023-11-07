// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#include <include/utils/utf8/utf8converter.h>

#include <codecvt>
#include <locale>

namespace utils::utf8
{
// Unix compilers will produce a 32-bit wstring as opposed to 16-bit on Windows
std::wstring WidenUtf8(const std::string& str)
{
  // NOLINTNEXTLINE: codecvt_utf8 is deprecated
  std::wstring_convert<std::codecvt_utf8<wchar_t>> utf8conv;
  std::wstring conv = utf8conv.from_bytes(str);
  return conv;
}

}  // namespace utils::utf8
