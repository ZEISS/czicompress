// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once
#include <memory>
#include <string>

#include "command.h"
#include "compressionstrategy.h"
#include "inc_libCZI.h"

class IConsoleIo;

/// This class is used to parse the command line arguments.
class CommandLineOptions
{
private:
  static const char* const kDefaultCompressionOptions;  ///< (Immutable) The default
                                                        ///< compression options.

  std::shared_ptr<IConsoleIo> log_;
  bool validation_mode_for_unittests_;  ///< If true, we are informed that we
                                        ///< are running in "unit-test" mode,
                                        ///< and we should adjust the validation
                                        ///< behavior accordingly.
  Command command_{Command::kInvalid};
  std::string input_filename_;
  std::string output_filename_;
  CompressionStrategy compression_strategy_{CompressionStrategy::kInvalid};
  libCZI::Utils::CompressionOption compression_option_;
  bool overwrite_existing_file_{false};
  bool ignore_duplicate_subblocks_{true};
public:
  /// Values that represent the result of the "Parse"-operation.
  enum class ParseResult
  {
    kOk,    ///< An enum constant representing the result "arguments successfully
            ///< parsed, operation can start".
    kExit,  ///< An enum constant representing the result "operation complete,
            ///< the program should now be terminated, e.g. the synopsis was
            ///< printed".
    kError  ///< An enum constant representing the result "the was an error
            ///< parsing the command line arguments, program should terminate".
  };

  /// Constructor of the command line options parser object. A log object must
  /// be provided, which is used to print messages to the console. If the
  /// 'validation_for_unittests' parameter is set to true, then some parameters
  /// are adjusted in order to allow for proper unit-testing of this class -
  /// e.g. the check whether the input file exists is disabled.
  ///
  /// \param  log                         The log.
  /// \param  validation_for_unittests    (Optional) True to inform class that
  /// it is used in "unit-test mode".
  explicit CommandLineOptions(std::shared_ptr<IConsoleIo> log, bool validation_for_unittests = false);

  /// Parses the command line arguments. The arguments are expected to be given
  /// in UTF8-encoding. This method handles some operations like "printing the
  /// help text" internally, and in such cases (where the is no additional
  /// operation to take place), the value 'ParseResult::Exit' is returned.
  ///
  /// \param          argc    The number of arguments.
  /// \param [in]     argv    An array containing the arguments.
  ///
  /// \returns    An enum indicating the result.
  ParseResult Parse(int argc, const char* const* argv);

  /// Gets the command or verb that was specified on the command line.
  ///
  /// \returns    The command.
  Command GetCommand() const { return this->command_; }

  /// Gets the input file name. This string uses UTF8-encoding.
  ///
  /// \returns    The input file name (in UTF8-encoding).
  const std::string& GetInputFileName() const { return this->input_filename_; }

  /// Gets the destination file name. This string uses UTF8-encoding.
  ///
  /// \returns    The input file name (in UTF8-encoding)
  const std::string& GetOutputFileName() const { return this->output_filename_; }

  /// Gets the compression strategy
  ///
  /// \returns    The compression strategy.
  CompressionStrategy GetCompressionStrategy() const { return this->compression_strategy_; }

  /// Gets the compression option - either the default option, or the one
  /// specified on the command line.
  ///
  /// \returns    The compression option.
  const libCZI::Utils::CompressionOption& GetCompressionOption() const { return this->compression_option_; }

  bool GetOverwriteExistingFile() const { return this->overwrite_existing_file_; }

  bool GetIgnoreDuplicateSubblocks() const { return this->ignore_duplicate_subblocks_; }

private:
  static std::string GetFooterText();
};
