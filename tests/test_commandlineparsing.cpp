// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#define CATCH_CONFIG_MAIN

#include <include/IConsoleio.h>
#include <include/commandlineoptions.h>

#include <catch2/catch_test_macros.hpp>
#include <tuple>
#include <vector>

#include "catch2/catch_all.hpp"

class ConsoleIoMock : public IConsoleIo
{
  bool IsStdOutATerminal() const override { return true; }
  void SetColor(ConsoleColor foreground, ConsoleColor background) override {}
  void MoveUp(int lines_to_move_up) override {}
  void WriteLineStdOut(const char* str) override {}
  void WriteLineStdOut(const wchar_t* str) override {}
  void WriteLineStdErr(const char* str) override {}
  void WriteLineStdErr(const wchar_t* str) override {}
  void WriteStdOut(const char* str) override {}
  void WriteStdOut(const wchar_t* str) override {}
  void WriteStdErr(const char* str) override {}
  void WriteStdErr(const wchar_t* str) override {}
};

// See here
// https://github.com/JohnnyHendriks/TestAdapter_Catch2/blob/main/Docs/Capabilities.md
// for the semantic of name given with TEST_CASE. And according to
// https://github.com/catchorg/Catch2/blob/devel/docs/test-cases-and-sections.md#top,
// the combination of name and tag must be unique.
TEST_CASE("commandlineparser.1: command-line arguments are parsed correctly", "[commandlineparser]")
{
  auto consoleIo = std::make_shared<ConsoleIoMock>();
  CommandLineOptions options(consoleIo, true);
  static const char* const argv[] =  // NOLINT: C-style array
      {"dummy", "--command", "compress", "--input", "input.czi", "--output", "output.czi"};

  const auto parse_result = options.Parse(static_cast<int>(std::size(argv)),
                                          argv);  // NOLINT: array to pointer decay

  REQUIRE(parse_result == CommandLineOptions::ParseResult::kOk);
  REQUIRE(options.GetCommand() == Command::kCompress);
  REQUIRE(options.GetInputFileName() == "input.czi");
  REQUIRE(options.GetOutputFileName() == "output.czi");
}

TEST_CASE("commandlineparser.2: command-line arguments are parsed correctly", "[commandlineparser]")
{
  auto consoleIo = std::make_shared<ConsoleIoMock>();
  CommandLineOptions options(consoleIo, true);

  static const char* argv[] =  // NOLINT: C-style array
      {"dummy",
       "--command",
       "compress",
       "--input",
       "input.czi",
       "--output",
       "output.czi",
       "--strategy",
       "uncompressed_and_zstd",
       "--compression_options",
       "zstd1:ExplicitLevel=4;PreProcess=HiLoByteUnpack"};

  const auto parse_result = options.Parse(static_cast<int>(std::size(argv)),
                                          argv);  // NOLINT: array to pointer decay

  REQUIRE(parse_result == CommandLineOptions::ParseResult::kOk);
  REQUIRE(options.GetCommand() == Command::kCompress);
  REQUIRE(options.GetInputFileName() == "input.czi");
  REQUIRE(options.GetOutputFileName() == "output.czi");
  REQUIRE(options.GetCompressionStrategy() == CompressionStrategy::kUncompressedAndZStdCompressed);
  REQUIRE(std::get<0>(options.GetCompressionOption()) == libCZI::CompressionMode::Zstd1);
  const auto& compression_parameters = std::get<1>(options.GetCompressionOption());
  libCZI::CompressParameter parameter;
  REQUIRE(compression_parameters->TryGetProperty(libCZI::CompressionParameterKey::ZSTD_RAWCOMPRESSIONLEVEL, &parameter) == true);
  REQUIRE(parameter.GetInt32() == 4);
  REQUIRE(compression_parameters->TryGetProperty(libCZI::CompressionParameterKey::ZSTD_PREPROCESS_DOLOHIBYTEPACKING, &parameter) == true);
  REQUIRE(parameter.GetBoolean() == true);
}
