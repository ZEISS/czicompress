// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#include <Windows.h>
#include <include/utils/errorhandling/winerrorformatting.h>
#include <include/utils/utf8/utf8converter.h>

#include <stdexcept>

namespace utils::utf8
{
std::wstring WidenUtf8(const std::string &str)
{
  if (str.empty())
  {
    return L"";
  }

  // This will fail if invalid input characters are present w/ MB_ERR_INVALID_CHARS rather than replacing/dropping
  const int size = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, str.c_str(), static_cast<int>(str.size()), nullptr, 0);

  if (size <= 0)
  {
    const auto error_msg = utils::errorhandling::GetReadableLastError();
    std::string error_string = "Failed to convert UTF-8 string to wstring : ";
    error_string.append(error_msg);
    throw std::runtime_error(error_string);
  }

  std::wstring result(size, 0);
  MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, str.c_str(), static_cast<int>(str.size()), result.data(),
                      static_cast<int>(result.size()));
  return result;
}
}  // namespace utils::utf8
