// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once

#include <memory>
#include <string>

/// Values that represent console colors.
enum class ConsoleColor : unsigned char
{
  BLACK = 0,
  DARK_BLUE,
  DARK_GREEN,
  DARK_CYAN,
  DARK_RED,
  DARK_MAGENTA,
  DARK_YELLOW,
  DARK_WHITE,
  LIGHT_BLACK,
  LIGHT_BLUE,
  LIGHT_GREEN,
  LIGHT_CYAN,
  LIGHT_RED,
  LIGHT_MAGENTA,
  LIGHT_YELLOW,
  WHITE,
  DEFAULT
};

/// This interface is used to write to the console. It is intended to provide
/// platform independent ability to change the color of the text on the console.
class IConsoleIo
{
public:
  /// Query if StdOut is a terminal, meaning that we can move the cursor and use
  /// colors.
  ///
  /// \returns    True if stdout a terminal, false if not.
  virtual bool IsStdOutATerminal() const = 0;

  virtual void SetColor(ConsoleColor foreground, ConsoleColor background) = 0;

  virtual void MoveUp(int lines_to_move_up) = 0;

  virtual void WriteLineStdOut(const char* sz) = 0;
  virtual void WriteLineStdOut(const wchar_t* sz) = 0;
  virtual void WriteLineStdErr(const char* sz) = 0;
  virtual void WriteLineStdErr(const wchar_t* sz) = 0;

  virtual void WriteStdOut(const char* sz) = 0;
  virtual void WriteStdOut(const wchar_t* sz) = 0;
  virtual void WriteStdErr(const char* sz) = 0;
  virtual void WriteStdErr(const wchar_t* sz) = 0;

  void WriteLineStdOut(const std::string& str) { this->WriteLineStdOut(str.c_str()); }

  void WriteLineStdOut(const std::wstring& str) { this->WriteLineStdOut(str.c_str()); }

  void WriteLineStdErr(const std::string& str) { this->WriteLineStdErr(str.c_str()); }

  void WriteLineStdErr(const std::wstring& str) { this->WriteLineStdErr(str.c_str()); }

  void WriteStdOut(const std::string& str) { this->WriteStdOut(str.c_str()); }

  void WriteStdOut(const std::wstring& str) { this->WriteStdOut(str.c_str()); }

  void WriteStdErr(const std::string& str) { this->WriteStdErr(str.c_str()); }

  void WriteStdErr(const std::wstring& str) { this->WriteStdErr(str.c_str()); }

  virtual ~IConsoleIo() = default;

  // non-copyable and non-moveable
  IConsoleIo() = default;
  IConsoleIo(const IConsoleIo&) = default;             // copy constructor
  IConsoleIo& operator=(const IConsoleIo&) = default;  // copy assignment
  IConsoleIo(IConsoleIo&&) = default;                  // move constructor
  IConsoleIo& operator=(IConsoleIo&&) = default;       // move assignment
};

std::shared_ptr<IConsoleIo> CreateConsoleIo();
