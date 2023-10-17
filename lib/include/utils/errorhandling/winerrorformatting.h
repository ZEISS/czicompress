// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once
#include <Windows.h>

#include <string>
namespace utils::errorhandling
{
std::string GetReadableErrorMessage(const DWORD error_code);
inline std::string GetReadableLastError() { return GetReadableErrorMessage(GetLastError()); }
}  // namespace utils::errorhandling
