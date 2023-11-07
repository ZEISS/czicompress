// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once

#include <CZICompress_Config.h>

#include <cstdint>
#include <memory>

#include "include/IConsoleio.h"

#if CZICOMPRESS_WIN32_ENVIRONMENT
#include <Windows.h>
#endif

/// Implementation of the ILog interface that writes to the console. This
/// implementation is intended to provide platform independent ability to change
/// the color of the text on the console. This includes older Windows version
/// that do not support ANSI escape sequences (aka Virtual Terminal Sequences,
/// c.f. https://learn.microsoft.com/en-us/windows/console/classic-vs-vt).
/// If stdout/stderr is redirected to a file, the color information is ignored.
class ConsoleIo : public IConsoleIo
{
private:
#if CZICOMPRESS_WIN32_ENVIRONMENT
  HANDLE console_handle_;
  std::uint16_t default_console_color_;
  bool can_use_virtual_terminal_sequences_;
#endif
#if CZICOMPRESS_UNIX_ENVIRONMENT
  bool is_terminal_output_;
#endif

public:
  ConsoleIo();

  bool IsStdOutATerminal() const override;

  void SetColor(ConsoleColor foreground, ConsoleColor background) override;

  void MoveUp(int lines_to_move_up) override;

  void WriteLineStdOut(const char* str) override;
  void WriteLineStdOut(const wchar_t* str) override;
  void WriteLineStdErr(const char* str) override;
  void WriteLineStdErr(const wchar_t* str) override;

  void WriteStdOut(const char* str) override;
  void WriteStdOut(const wchar_t* str) override;
  void WriteStdErr(const char* str) override;
  void WriteStdErr(const wchar_t* str) override;

private:
#if CZICOMPRESS_WIN32_ENVIRONMENT
  std::uint16_t GetColorAttribute(ConsoleColor foreground, ConsoleColor background) const;
#endif
#if CZICOMPRESS_UNIX_ENVIRONMENT
  void SetTextColorAnsi(ConsoleColor foreground, ConsoleColor background);
#endif
};
