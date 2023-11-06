// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#include "commandlineargshelper.h"

#if CZICOMPRESS_WIN32_ENVIRONMENT
#include <Windows.h>

#include <codecvt>
#include <memory>
#include <string>
#include <vector>

CommandlineArgsWindowsHelper::CommandlineArgsWindowsHelper()
{
  int number_arguments = 0;
  const std::unique_ptr<LPWSTR, decltype(LocalFree)*> wide_argv{CommandLineToArgvW(GetCommandLineW(), &number_arguments), &LocalFree};

  this->pointers_to_arguments_.reserve(number_arguments);
  this->arguments_.reserve(number_arguments);

  for (int i = 0; i < number_arguments; ++i)
  {
    // NOLINTNEXTLINE: codecvt is deprecated
    std::wstring_convert<std::codecvt_utf8<wchar_t>> utf8_conv;
    const std::string conv = utf8_conv.to_bytes(wide_argv.get()[i]);
    this->arguments_.emplace_back(conv);
  }

  for (int i = 0; i < number_arguments; ++i)
  {
    this->pointers_to_arguments_.emplace_back(this->arguments_[i].data());
  }
}

const char* const* CommandlineArgsWindowsHelper::GetArgv() const { return this->pointers_to_arguments_.data(); }

int CommandlineArgsWindowsHelper::GetArgc() const { return static_cast<int>(this->pointers_to_arguments_.size()); }
#endif  // CZICOMPRESS_WIN32_ENVIRONMENT
