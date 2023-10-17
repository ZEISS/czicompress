// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#include <CZICompress_Config.h>
#include <include/IConsoleio.h>
#include <include/commandlineoptions.h>
#include <include/utils/utf8/utf8converter.h>

#include <memory>

#include "commandlineargshelper.h"
#include "inc_libCZI.h"
#include "include/IOperation.h"
#if CZICOMPRESS_WIN32_ENVIRONMENT
#include <Windows.h>
#endif
#include <iostream>

/// This structure is used in the progress-callback (to keep track of the
/// previous phase).
struct PrintProgressState
{
  ProcessingPhase previous_phase{ProcessingPhase::kInvalid};  ///< The previous phase we have seen in the
                                                              ///< progress-callback.
};

static void PrintProgress(const std::shared_ptr<IConsoleIo>& console_io, PrintProgressState& print_progress_state,
                          const ProgressInfo& info);

int main(int argc, char** argv)
{
#if CZICOMPRESS_WIN32_ENVIRONMENT
  SetConsoleOutputCP(CP_UTF8);
#endif

  auto console_io = CreateConsoleIo();
  CommandLineOptions command_line_options(console_io);
  CommandLineOptions::ParseResult parse_result = CommandLineOptions::ParseResult::kError;
  try
  {
#if CZICOMPRESS_WIN32_ENVIRONMENT
    (void)argc;
    (void)argv;
    const CommandlineArgsWindowsHelper args_helper;
    parse_result = command_line_options.Parse(args_helper.GetArgc(), args_helper.GetArgv());
#endif
#if CZICOMPRESS_UNIX_ENVIRONMENT
    setlocale(LC_CTYPE, "");
    parse_result = command_line_options.Parse(argc, argv);
#endif
  }
  catch (std::exception& exception)
  {
    std::stringstream message;
    message << "Error parsing the command-line: " << exception.what() << ".";
    console_io->WriteLineStdErr(message.str());
    return EXIT_FAILURE;
  }

  switch (parse_result)
  {
    case CommandLineOptions::ParseResult::kError:
      console_io->WriteLineStdErr("There was an error parsing the command line -> exiting");
      return EXIT_FAILURE;
    case CommandLineOptions::ParseResult::kExit:
      return EXIT_SUCCESS;
    case CommandLineOptions::ParseResult::kOk:
      break;
  }

#if CZICOMPRESS_WIN32_ENVIRONMENT
  CoInitialize(NULL);

  // on Windows, we want to use the WIC-JpgXR-decoder - this must be done BEFORE
  // the first call into libCZI
  SetSiteObject(GetDefaultSiteObject(libCZI::SiteObjectType::WithWICDecoder));
#endif

  int return_code = EXIT_SUCCESS;
  try
  {
    // create the "input-stream-object"
    const auto stream = libCZI::CreateStreamFromFile(utils::utf8::WidenUtf8(command_line_options.GetInputFileName()).c_str());

    // create the "CZI-reader"-object
    const auto reader = libCZI::CreateCZIReader();

    // Note: we request "strict parsing" when opening the CZI, which will cause libCZI to bail out with an exception
    //        when encountering certain flaky variants of a CZI-document (esp. if "non-X-Y-subblocks" are present).
    libCZI::ICZIReader::OpenOptions open_options;
    open_options.lax_subblock_coordinate_checks = false;
    open_options.ignore_sizem_for_pyramid_subblocks = true;
    reader->Open(stream, &open_options);

    // Create an "output-stream-object"
    const auto output_stream =
        libCZI::CreateOutputStreamForFile(utils::utf8::WidenUtf8(command_line_options.GetOutputFileName()).c_str(), false);

    // create the "CZI-writer"-object
    const auto writer = libCZI::CreateCZIWriter();

    // GUID_NULL here means that a new Guid is created
    const auto czi_writer_info = std::make_shared<libCZI::CCziWriterInfo>(GUID{0, 0, 0, {0, 0, 0, 0, 0, 0, 0, 0}});

    // TODO(JBL): it might be desirable to make reservations for the
    //            subblock-directory-/attachments-directory-/metadata-segment
    //            (so that the end up at the beginning of the file instead of at
    //            the end), but this shouldn't make a difference in terms of
    //            proper operation
    writer->Create(output_stream, czi_writer_info);

    auto operation = CreateOperationUp();

    OperationDescription operation_description;
    operation_description.reader = reader;
    operation_description.writer = writer;
    operation_description.command = command_line_options.GetCommand();
    operation_description.compression_strategy = command_line_options.GetCompressionStrategy();
    operation_description.compression_option = command_line_options.GetCompressionOption();

    operation->SetParameters(operation_description);
    PrintProgressState print_progress_state;
    std::function<bool(const ProgressInfo&)> progress_callback;

    // if stdout is redirected to a file, we better don't print progress (as it
    // would flood the file)
    if (console_io->IsStdOutATerminal())
    {
      progress_callback = [&console_io, &print_progress_state](const ProgressInfo& info) -> bool
      {
        PrintProgress(console_io, print_progress_state, info);
        return true;
      };
    }

    operation->DoOperation(progress_callback);
    operation.reset();
    writer->Close();
  }
  catch (const std::exception& exception)
  {
    std::stringstream message;
    message << "An unrecoverable runtime error occurred: " << exception.what() << ".";
    console_io->WriteLineStdErr(message.str());
    return_code = EXIT_FAILURE;
  }

#if CZICOMPRESS_WIN32_ENVIRONMENT
  CoUninitialize();
#endif

  return return_code;
}

void PrintProgress(const std::shared_ptr<IConsoleIo>& console_io, PrintProgressState& print_progress_state, const ProgressInfo& info)
{
  std::stringstream string_stream;
  string_stream << ProcessingPhaseAsInformalString(info.phase);
  if (info.number_of_items_todo > 0)
  {
    string_stream << " " << info.number_of_items_done << "/" << info.number_of_items_todo;
  }
  else
  {
    string_stream << " " << info.number_of_items_done;
  }

  // overwrite the previous line if we are in the same phase only
  if (print_progress_state.previous_phase != ProcessingPhase::kInvalid && print_progress_state.previous_phase == info.phase)
  {
    console_io->MoveUp(1);
  }

  console_io->WriteLineStdOut(string_stream.str());
  print_progress_state.previous_phase = info.phase;
}
