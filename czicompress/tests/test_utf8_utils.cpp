// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#include <include/utils/utf8/utf8converter.h>
#include <src/copyczi.h>

#include <catch2/catch_test_macros.hpp>
#include <string>

#include "catch2/catch_all.hpp"
#include "libczi_utils.h"

TEST_CASE("utf8parsing.1: Length count", "[utf8parsing]")
{
  // Arrange
  std::string greek_string = "κόσμε";  // We expect byte size of 11 here

  // Act
  std::wstring wide_greek_string = utils::utf8::WidenUtf8(greek_string);

  // Assert
#ifdef _WIN32
  REQUIRE(wide_greek_string.size() * sizeof(wchar_t) == 10);
#elif __linux__
  REQUIRE(wide_greek_string.size() * sizeof(wchar_t) == 20);  // Linux impl creates UTF-32 instead of UTF-16
#else
  REQUIRE(wide_greek_string.size() == 5);  // We expect 5 characters for a 5 character utf-8 string
#endif
  REQUIRE(greek_string.size() == 11);
}

TEST_CASE("utf8parsing.2: Emoji test", "[utf8parsing]")
{
  // Arrange
  std::string emoji_string = "💻";  // We expect byte size of 4 here

  // Act
  std::wstring wide_emoji_string = utils::utf8::WidenUtf8(emoji_string);

  // Assert
#ifdef _WIN32
  REQUIRE(wide_emoji_string.size() * sizeof(wchar_t) == 4);
#elif __linux__
  REQUIRE(wide_emoji_string.size() * sizeof(wchar_t) == 4);
#else
  REQUIRE(wide_emoji_string.size() == 1);
#endif
  REQUIRE(emoji_string.size() == 4);
}

TEST_CASE("utf8parsing.3: Chinese character test", "[utf8parsing]")
{
  // Arrange
  std::string chinese_string = "測試串";  // We expect 9 utf-8 bytes here

  // Act
  std::wstring wide_chinese_string = utils::utf8::WidenUtf8(chinese_string);

  // Assert
#ifdef _WIN32
  REQUIRE(wide_chinese_string.size() * sizeof(wchar_t) == 6);
#elif __linux__
  REQUIRE(wide_chinese_string.size() * sizeof(wchar_t) == 12);
#else
  REQUIRE(wide_chinese_string.size() == 3);
#endif
  REQUIRE(chinese_string.size() == 9);
}
