// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#include "include/commandlineoptions.h"

#include <CZICompress_Config.h>

#include <CLI/CLI.hpp>
#include <map>
#include <memory>
#include <string>
#include <utility>
#include <vector>

#include "inc_libCZI.h"

using std::string, std::ostringstream, std::endl, std::istringstream, std::make_shared;

/// A custom formatter for CLI11 - used to have nicely formatted descriptions.
class CustomFormatter : public CLI::Formatter
{
private:
  static const int DEFAULT_COLUMN_WIDTH = 20;

public:
  CustomFormatter() { this->column_width(DEFAULT_COLUMN_WIDTH); }

  std::string make_usage(const CLI::App* app, std::string name) const override
  {
    // 'name' is the full path of the executable, we only take the path "after
    // the last slash or backslash"
    size_t offset = name.rfind('/');
    if (offset == string::npos)
    {
      offset = name.rfind('\\');
    }

    if (offset != string::npos && offset < name.length())
    {
      name = name.substr(1 + offset);
    }

    const auto result_from_stock_implementation = this->CLI::Formatter::make_usage(app, name);
    ostringstream usage(result_from_stock_implementation);
    usage << result_from_stock_implementation << endl
          << "  version: " << CZICOMPRESS_VERSION_MAJOR << "." << CZICOMPRESS_VERSION_MINOR << "." << CZICOMPRESS_VERSION_PATCH;
    std::string tweak(CZICOMPRESS_VERSION_TWEAK);
    if (!tweak.empty())
    {
      usage << "." << tweak;
    }
    usage << endl;
    return usage.str();
  }

  std::string make_footer(const CLI::App* app) const override
  {
    ostringstream footer;
    footer << Formatter::make_footer(app) << endl << endl;
    int major_version = 0;
    int minor_version = 0;
    int tweak_version = 0;
    libCZI::GetLibCZIVersion(&major_version, &minor_version, &tweak_version);
    footer << "using libCZI version " << major_version << "." << minor_version << "." << tweak_version << endl;
    return footer.str();
  }

  std::string make_option_desc(const CLI::Option* option) const override
  {
    // we wrap the text so that it fits in the "second column"
    const auto lines = wrap(option->get_description().c_str(), 80 - this->get_column_width());

    string options_description;
    options_description.reserve(accumulate(lines.cbegin(), lines.cend(), static_cast<size_t>(0),
                                           [](size_t sum, const std::string& str) { return 1 + sum + str.size(); }));
    for (const auto& line : lines)
    {
      options_description.append(line).append("\n");
    }

    return options_description;
  }

  static std::vector<std::string> wrap(const char* text, size_t line_length)
  {
    istringstream iss(text);
    std::string word;
    std::string line;
    std::vector<std::string> vec(0, "");

    for (;;)
    {
      iss >> word;
      if (!iss)
      {
        break;
      }

      // '\n' before a word means "insert a linebreak", and "\N' means "insert a
      // linebreak and one more empty line"
      if (word.size() > 2 && word[0] == '\\' && (word[1] == 'n' || word[1] == 'N'))
      {
        line.erase(line.size() - 1);  // remove trailing space
        vec.push_back(line);
        if (word[1] == 'N')
        {
          vec.emplace_back("");
        }

        line.clear();
        word.erase(0, 2);
      }

      if (line.length() + word.length() > line_length)
      {
        line.erase(line.size() - 1);
        vec.push_back(line);
        line.clear();
      }

      line += word + ' ';
    }

    if (!line.empty())
    {
      line.erase(line.size() - 1);
      vec.push_back(line);
    }

    return vec;
  }
};

/*static*/ const char* const CommandLineOptions::kDefaultCompressionOptions = "zstd1:ExplicitLevel=1;PreProcess=HiLoByteUnpack";

CommandLineOptions::CommandLineOptions(std::shared_ptr<IConsoleIo> log, bool validation_for_unittests /*= false*/)
    : log_(std::move(log)), validation_mode_for_unittests_(validation_for_unittests)
{
}

CommandLineOptions::ParseResult CommandLineOptions::Parse(int argc, const char* const* argv)
{
  CLI::App app{"czicompress"};

  Command command{Command::kInvalid};  // NOLINT(misc-const-correctness)
  CompressionStrategy                  // NOLINT(misc-const-correctness)
      compression_strategy{CompressionStrategy::kInvalid};
  string source_filename;           // NOLINT(misc-const-correctness)
  string destination_filename;      // NOLINT(misc-const-correctness)
  string compression_options_text;  // NOLINT(misc-const-correctness)
  bool overwrite_existing_file{false};
  bool ignore_duplicate_subblocks{false};

  // specify the string-to-enum-mapping for a boolean option
  std::map<std::string, bool> map_string_to_boolean{
      {"0", false}, {"false", false}, {"no", false}, {"1", true}, {"true", true}, {"yes", true},
  };

  // specify the string-to-enum-mapping for "command"
  const std::map<std::string, Command> map_string_to_command{{"compress", Command::kCompress}, {"decompress", Command::kDecompress}};

  // specify the string-to-enum-mapping for "compression strategy"
  const std::map<std::string, CompressionStrategy> map_string_to_strategy{
      {"all", CompressionStrategy::kAll},
      {"uncompressed", CompressionStrategy::kOnlyUncompressed},
      {"uncompressed_and_zstd", CompressionStrategy::kUncompressedAndZStdCompressed}};

  app.add_option("-c,--command", command,
                 "Specifies the mode of operation: "
                 "'compress' to convert to a zstd-compressed CZI, "
                 "'decompress' to convert to a CZI containing only uncompressed data.")
      ->option_text("COMMAND")
      ->required()
      ->transform(CLI::CheckedTransformer(map_string_to_command, CLI::ignore_case));

  CLI::Option* option =
      app.add_option("-i,--input", source_filename, "The source CZI-file to be processed.")->option_text("SOURCE_FILE")->required();
  if (!this->validation_mode_for_unittests_)
  {
    // in "unit-test mode", we don't want to check whether the file exists
    option->check(CLI::ExistingFile);
  }

  app.add_option("-o,--output", destination_filename, "The destination CZI-file to be written.")
      ->option_text("DESTINATION_FILE")
      ->required();

  app.add_option("-s,--strategy", compression_strategy,
                 "Choose which subblocks of the source file are compressed. "
                 "STRATEGY can be one of 'all', 'uncompressed', "
                 "'uncompressed_and_zstd'. The default is 'uncompressed'.")
      ->option_text("STRATEGY")
      ->default_val(CompressionStrategy::kOnlyUncompressed)
      ->transform(CLI::CheckedTransformer(map_string_to_strategy, CLI::ignore_case));

  app.add_option("-t,--compression_options", compression_options_text,
                 "Specify compression parameters. The default is "
                 "'zstd1:ExplicitLevel=1;PreProcess=HiLoByteUnpack'.")
      ->option_text("COMPRESSION_OPTIONS")
      ->default_val(CommandLineOptions::kDefaultCompressionOptions);

  app.add_flag("-w,--overwrite", overwrite_existing_file, "If the output file exists, try to overwrite it.");
  app.add_option("--ignore_duplicate_subblocks", ignore_duplicate_subblocks,
                 "If this option is enabled, the operation will ignore if duplicate subblocks are encountered in the source document. "
                 "Otherwise, an error will be reported. The default is 'on'.")
      ->option_text("BOOLEAN")
      ->default_val(true)
      ->transform(CLI::CheckedTransformer(map_string_to_boolean, CLI::ignore_case));

  const auto formatter = make_shared<CustomFormatter>();
  app.formatter(formatter);
  app.footer(CommandLineOptions::GetFooterText());

  try
  {
    app.parse(argc, argv);
  }
  catch (const CLI::CallForHelp& e)
  {
    app.exit(e);
    return ParseResult::kExit;
  }
  catch (const CLI::ParseError& e)
  {
    app.exit(e);
    return ParseResult::kError;
  }

  this->command_ = command;
  this->input_filename_ = source_filename;
  this->output_filename_ = destination_filename;

  if (this->command_ == Command::kCompress)
  {
    this->compression_strategy_ = compression_strategy;
    this->compression_option_ = libCZI::Utils::ParseCompressionOptions(compression_options_text);
  }

  this->overwrite_existing_file_ = overwrite_existing_file;
  this->ignore_duplicate_subblocks_ = ignore_duplicate_subblocks;

  return CommandLineOptions::ParseResult::kOk;
}

string CommandLineOptions::GetFooterText()
{
  static const char* footer_text = R"(
Copies the content of a CZI-file into another CZI-file changing the compression of the image data. 

\nWith the 'compress' command, uncompressed image data is converted to Zstd-compressed image data. This can reduce the
file size substantially.
With the 'decompress' command, compressed image data is converted to uncompressed data.

\nFor the 'compress' command, a compression strategy can be specified with the '--strategy' option. It controls which
subblocks of the source file will be compressed. The source document may already contain compressed data (possibly
with a lossy compression scheme). In this case it is undesirable to compress the data with lossless zstd, as that will
almost certainly increase the file size. Therefore, the "uncompressed" strategy compresses only uncompressed subblocks.
The "uncompressed_and_zstd" strategy compresses the subblocks that are uncompressed OR compressed with Zstd, and the
"all" strategy compresses all subblocks, regardless of their current compression status.
Some compression schemes that can occur in a CZI-file cannot be decompressed by this tool. Data compressed with such a
scheme will be copied verbatim to the destination file, regardless of the command and strategy chosen.
)";

  const auto lines = CustomFormatter::wrap(footer_text, 80);

  string text;
  text.reserve(accumulate(lines.cbegin(), lines.cend(), static_cast<size_t>(0),
                          [](size_t sum, const std::string& str) { return 1 + sum + str.size(); }));
  for (const auto& line : lines)
  {
    text.append(line).append("\n");
  }

  return text;
}
