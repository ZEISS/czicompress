// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#include <Windows.h>
#include <include/utils/errorhandling/winerrorformatting.h>

#include <string>

std::string utils::errorhandling::GetReadableErrorMessage(const DWORD error_code)
{
  LPSTR message_buffer;

  // This will allocate to the buffer since size can be arbitrary
  // Please refer to https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-formatmessagea for specifics
  const auto length = FormatMessageA(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM |
                                         FORMAT_MESSAGE_IGNORE_INSERTS,  // Allocates provided buffer | search the system message-table
                                                                         // resource | Prevents security issue w. abusing inserts
                                     NULL,        // We are not using lpsource to define message table nor message definition
                                     error_code,  // Our given error code
                                     0,  // We do not want to define a language identifier, rather default to neutral or user defined
                                     (LPSTR)&message_buffer,  // target buffer to place message from message table
                                     0,                       // windows will allocate buffer so we do not need to pass in anything here
                                     NULL);                   // We are not leveraging formatted msg arguments

  // Note that FormatMessageA can fail in which was we will have to use GetLastError to get specifics....

  std::string human_readable_message(message_buffer, length);

  // free allocated string buffer
  LocalFree(message_buffer);

  return human_readable_message;
}
