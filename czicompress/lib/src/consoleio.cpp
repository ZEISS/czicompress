// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#define NOMINMAX  // Don't import min and max from Windows.h
#include "consoleio.h"

#include <CZICompress_Config.h>

#include <cstdarg>
#include <iostream>
#include <limits>
#include <memory>

#if CZICOMPRESS_WIN32_ENVIRONMENT
#include <Windows.h>
#include <consoleapi2.h>
#include <io.h>
#endif
#if CZICOMPRESS_UNIX_ENVIRONMENT
#include <unistd.h>
#endif

std::shared_ptr<IConsoleIo> CreateConsoleIo() { return std::make_shared<ConsoleIo>(); }

ConsoleIo::ConsoleIo()
#if CZICOMPRESS_WIN32_ENVIRONMENT
    : console_handle_(INVALID_HANDLE_VALUE),
      can_use_virtual_terminal_sequences_(false)
#endif
#if CZICOMPRESS_UNIX_ENVIRONMENT
    : is_terminal_output_(false)
#endif
{
#if CZICOMPRESS_WIN32_ENVIRONMENT
  const int stdoutFile = _fileno(stdout);

  // c.f.
  // https://docs.microsoft.com/en-us/cpp/c-runtime-library/reference/get-osfhandle?view=msvc-170&viewFallbackFrom=vs-2017
  // , -2 is a magic value indicating no association with a stream
  if (stdoutFile != -2)
  {
    const intptr_t osfhandle = _get_osfhandle(stdoutFile);
    auto* consoleHandle = reinterpret_cast<HANDLE>(osfhandle);  // NOLINT
    if (consoleHandle != INVALID_HANDLE_VALUE)
    {
      const DWORD file_type = GetFileType(consoleHandle);
      if (file_type == FILE_TYPE_CHAR)
      {
        CONSOLE_SCREEN_BUFFER_INFO screenBufferInfo;
        GetConsoleScreenBufferInfo(consoleHandle, &screenBufferInfo);
        this->default_console_color_ = screenBufferInfo.wAttributes;
        this->console_handle_ = consoleHandle;
        DWORD mode = 0;
        const BOOL success = GetConsoleMode(this->console_handle_, &mode);
        if (success != FALSE && (mode & ENABLE_VIRTUAL_TERMINAL_PROCESSING) != 0)
        {
          this->can_use_virtual_terminal_sequences_ = true;
        }
      }
    }
  }
#endif
#if CZICOMPRESS_UNIX_ENVIRONMENT
  this->is_terminal_output_ = isatty(fileno(stdout)) == 1;
#endif
}

bool ConsoleIo::IsStdOutATerminal() const
{
#if CZICOMPRESS_WIN32_ENVIRONMENT
  return this->console_handle_ != INVALID_HANDLE_VALUE;
#endif
#if CZICOMPRESS_UNIX_ENVIRONMENT
  return this->is_terminal_output_;
#endif
}

void ConsoleIo::SetColor(ConsoleColor foreground, ConsoleColor background)
{
#if CZICOMPRESS_WIN32_ENVIRONMENT
  if (this->console_handle_ != INVALID_HANDLE_VALUE)
  {
    SetConsoleTextAttribute(this->console_handle_, this->GetColorAttribute(foreground, background));
  }
#endif
#if CZICOMPRESS_UNIX_ENVIRONMENT
  if (this->is_terminal_output_)
  {
    this->SetTextColorAnsi(foreground, background);
  }
#endif
}

void ConsoleIo::WriteLineStdOut(const char* str) { std::cout << str << std::endl; }

void ConsoleIo::WriteLineStdOut(const wchar_t* str) { std::wcout << str << std::endl; }

void ConsoleIo::WriteLineStdErr(const char* str) { std::cout << str << std::endl; }

void ConsoleIo::WriteLineStdErr(const wchar_t* str) { std::wcout << str << std::endl; }

void ConsoleIo::WriteStdOut(const char* str) { std::cout << str; }

void ConsoleIo::WriteStdOut(const wchar_t* str) { std::wcout << str; }

void ConsoleIo::WriteStdErr(const char* str) { std::cout << str; }

void ConsoleIo::WriteStdErr(const wchar_t* str) { std::wcout << str; }

void ConsoleIo::MoveUp(int lines_to_move_up)
{
#if CZICOMPRESS_UNIX_ENVIRONMENT
  std::cout << "\033[" << lines_to_move_up << "A";
#endif
#if CZICOMPRESS_WIN32_ENVIRONMENT
  if (this->can_use_virtual_terminal_sequences_)
  {
    std::cout << "\033[" << lines_to_move_up << "A";
  }
  else
  {
    CONSOLE_SCREEN_BUFFER_INFO consoleScreenBufferInfo;
    const BOOL success = GetConsoleScreenBufferInfo(this->console_handle_, &consoleScreenBufferInfo);
    if (success == FALSE)
    {
      return;
    }

    COORD coord = consoleScreenBufferInfo.dwCursorPosition;

    // NOLINTNEXTLINE: narrowing conversion is OK here
    coord.Y -= static_cast<SHORT>(std::max(lines_to_move_up, static_cast<int>(std::numeric_limits<SHORT>::max())));
    SetConsoleCursorPosition(this->console_handle_, coord);
  }
#endif
}

#if CZICOMPRESS_WIN32_ENVIRONMENT
std::uint16_t ConsoleIo::GetColorAttribute(ConsoleColor foreground, ConsoleColor background) const
{
  std::uint16_t attribute = 0;
  switch (foreground)
  {
    case ConsoleColor::BLACK:
      attribute = 0;
      break;
    case ConsoleColor::DARK_BLUE:
      attribute = FOREGROUND_BLUE;
      break;
    case ConsoleColor::DARK_GREEN:
      attribute = FOREGROUND_GREEN;
      break;
    case ConsoleColor::DARK_CYAN:
      attribute = FOREGROUND_GREEN | FOREGROUND_BLUE;
      break;
    case ConsoleColor::DARK_RED:
      attribute = FOREGROUND_RED;
      break;
    case ConsoleColor::DARK_MAGENTA:
      attribute = FOREGROUND_RED | FOREGROUND_BLUE;
      break;
    case ConsoleColor::DARK_YELLOW:
      attribute = FOREGROUND_RED | FOREGROUND_GREEN;
      break;
    case ConsoleColor::DARK_WHITE:
      attribute = FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE;
      break;
    case ConsoleColor::LIGHT_BLACK:
      attribute = FOREGROUND_INTENSITY;
      break;
    case ConsoleColor::LIGHT_BLUE:
      attribute = FOREGROUND_INTENSITY | FOREGROUND_BLUE;
      break;
    case ConsoleColor::LIGHT_GREEN:
      attribute = FOREGROUND_INTENSITY | FOREGROUND_GREEN;
      break;
    case ConsoleColor::LIGHT_CYAN:
      attribute = FOREGROUND_INTENSITY | FOREGROUND_GREEN | FOREGROUND_BLUE;
      break;
    case ConsoleColor::LIGHT_RED:
      attribute = FOREGROUND_INTENSITY | FOREGROUND_RED;
      break;
    case ConsoleColor::LIGHT_MAGENTA:
      attribute = FOREGROUND_INTENSITY | FOREGROUND_RED | FOREGROUND_BLUE;
      break;
    case ConsoleColor::LIGHT_YELLOW:
      attribute = FOREGROUND_INTENSITY | FOREGROUND_RED | FOREGROUND_GREEN;
      break;
    case ConsoleColor::WHITE:
      attribute = FOREGROUND_INTENSITY | FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE;
      break;
    case ConsoleColor::DEFAULT:
      attribute = (this->default_console_color_) & (FOREGROUND_INTENSITY | FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
      break;
  }

  switch (background)
  {
    case ConsoleColor::BLACK:
      attribute |= 0;
      break;
    case ConsoleColor::DARK_BLUE:
      attribute |= BACKGROUND_BLUE;
      break;
    case ConsoleColor::DARK_GREEN:
      attribute |= BACKGROUND_GREEN;
      break;
    case ConsoleColor::DARK_CYAN:
      attribute |= BACKGROUND_GREEN | BACKGROUND_BLUE;
      break;
    case ConsoleColor::DARK_RED:
      attribute |= BACKGROUND_RED;
      break;
    case ConsoleColor::DARK_MAGENTA:
      attribute |= BACKGROUND_RED | BACKGROUND_BLUE;
      break;
    case ConsoleColor::DARK_YELLOW:
      attribute |= BACKGROUND_RED | BACKGROUND_GREEN;
      break;
    case ConsoleColor::DARK_WHITE:
      attribute |= BACKGROUND_RED | BACKGROUND_GREEN | BACKGROUND_BLUE;
      break;
    case ConsoleColor::LIGHT_BLACK:
      attribute |= BACKGROUND_INTENSITY;
      break;
    case ConsoleColor::LIGHT_BLUE:
      attribute |= BACKGROUND_INTENSITY | BACKGROUND_BLUE;
      break;
    case ConsoleColor::LIGHT_GREEN:
      attribute |= BACKGROUND_INTENSITY | BACKGROUND_GREEN;
      break;
    case ConsoleColor::LIGHT_CYAN:
      attribute |= BACKGROUND_INTENSITY | BACKGROUND_GREEN | BACKGROUND_BLUE;
      break;
    case ConsoleColor::LIGHT_RED:
      attribute |= BACKGROUND_INTENSITY | BACKGROUND_RED;
      break;
    case ConsoleColor::LIGHT_MAGENTA:
      attribute |= BACKGROUND_INTENSITY | BACKGROUND_RED | BACKGROUND_BLUE;
      break;
    case ConsoleColor::LIGHT_YELLOW:
      attribute |= BACKGROUND_INTENSITY | BACKGROUND_RED | BACKGROUND_GREEN;
      break;
    case ConsoleColor::WHITE:
      attribute |= BACKGROUND_INTENSITY | BACKGROUND_RED | BACKGROUND_GREEN | BACKGROUND_BLUE;
      break;
    case ConsoleColor::DEFAULT:
      attribute |= (this->default_console_color_ & (BACKGROUND_INTENSITY | BACKGROUND_RED | BACKGROUND_GREEN | BACKGROUND_BLUE));
      break;
  }

  return attribute;
}
#endif

#if CZICOMPRESS_UNIX_ENVIRONMENT
void ConsoleIo::SetTextColorAnsi(ConsoleColor foreground, ConsoleColor background)
{
  const char* ansiForeground;
  const char* ansiBackground;
  switch (foreground)
  {
    case ConsoleColor::BLACK:
      ansiForeground = "\033[30m";
      break;
    case ConsoleColor::DARK_BLUE:
      ansiForeground = "\033[34m";
      break;
    case ConsoleColor::DARK_GREEN:
      ansiForeground = "\033[32m";
      break;
    case ConsoleColor::DARK_CYAN:
      ansiForeground = "\033[36m";
      break;
    case ConsoleColor::DARK_RED:
      ansiForeground = "\033[31m";
      break;
    case ConsoleColor::DARK_MAGENTA:
      ansiForeground = "\033[35m";
      break;
    case ConsoleColor::DARK_YELLOW:
      ansiForeground = "\033[33m";
      break;
    case ConsoleColor::DARK_WHITE:
      ansiForeground = "\033[37m";
      break;
    case ConsoleColor::LIGHT_BLACK:
      ansiForeground = "\033[90m";
      break;
    case ConsoleColor::LIGHT_BLUE:
      ansiForeground = "\033[94m";
      break;
    case ConsoleColor::LIGHT_GREEN:
      ansiForeground = "\033[92m";
      break;
    case ConsoleColor::LIGHT_CYAN:
      ansiForeground = "\033[96m";
      break;
    case ConsoleColor::LIGHT_RED:
      ansiForeground = "\033[91m";
      break;
    case ConsoleColor::LIGHT_MAGENTA:
      ansiForeground = "\033[95m";
      break;
    case ConsoleColor::LIGHT_YELLOW:
      ansiForeground = "\033[93m";
      break;
    case ConsoleColor::WHITE:
      ansiForeground = "\033[97m";
      break;
    case ConsoleColor::DEFAULT:
      ansiForeground = "\033[39m";
      break;
    default:
      ansiForeground = "";
      break;
  }

  switch (background)
  {
    case ConsoleColor::BLACK:
      ansiBackground = "\033[40m";
      break;
    case ConsoleColor::DARK_BLUE:
      ansiBackground = "\033[44m";
      break;
    case ConsoleColor::DARK_GREEN:
      ansiBackground = "\033[42m";
      break;
    case ConsoleColor::DARK_CYAN:
      ansiBackground = "\033[46m";
      break;
    case ConsoleColor::DARK_RED:
      ansiBackground = "\033[41m";
      break;
    case ConsoleColor::DARK_MAGENTA:
      ansiBackground = "\033[45m";
      break;
    case ConsoleColor::DARK_YELLOW:
      ansiBackground = "\033[43m";
      break;
    case ConsoleColor::DARK_WHITE:
      ansiBackground = "\033[47m";
      break;
    case ConsoleColor::LIGHT_BLACK:
      ansiBackground = "\033[100m";
      break;
    case ConsoleColor::LIGHT_BLUE:
      ansiBackground = "\033[104m";
      break;
    case ConsoleColor::LIGHT_GREEN:
      ansiBackground = "\033[102m";
      break;
    case ConsoleColor::LIGHT_CYAN:
      ansiBackground = "\033[106m";
      break;
    case ConsoleColor::LIGHT_RED:
      ansiBackground = "\033[101m";
      break;
    case ConsoleColor::LIGHT_MAGENTA:
      ansiBackground = "\033[105m";
      break;
    case ConsoleColor::LIGHT_YELLOW:
      ansiBackground = "\033[103m";
      break;
    case ConsoleColor::WHITE:
      ansiBackground = "\033[107m";
      break;
    case ConsoleColor::DEFAULT:
      ansiBackground = "\033[49m";
      break;
    default:
      ansiBackground = "";
      break;
  }

  std::cout << ansiForeground << ansiBackground;
}
#endif
