// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#include "capi.h"

#include <CZICompress_Config.h>
#include <include/utils/utf8/utf8converter.h>

#include <memory>
#include <string>

#include "inc_libCZI.h"
#include "include/IOperation.h"

#if CZICOMPRESS_WIN32_ENVIRONMENT

#include <Windows.h>

#endif

class FileProcessor final
{
private:
  Command command_;
  CompressionStrategy compression_strategy_;
  int compression_level_;

public:
  FileProcessor(Command command, CompressionStrategy strategy, int compression_level)
      : command_(command), compression_strategy_(strategy), compression_level_(compression_level)
  {
  }

  void ProcessFile(const char *const input_path, const char *const output_path, ProgressReport progress_report)
  {
    // create the "CZI-reader"-object
    const std::string input_string(input_path);
    const auto stream = libCZI::CreateStreamFromFile(utils::utf8::WidenUtf8(input_string).c_str());
    const auto reader = libCZI::CreateCZIReader();

    // Note: we request "strict parsing" when opening the CZI, which will cause libCZI to bail out with an exception
    //        when encountering certain flaky variants of a CZI-document (esp. if "non-X-Y-subblocks" are present)
    libCZI::ICZIReader::OpenOptions open_options;
    open_options.lax_subblock_coordinate_checks = false;
    open_options.ignore_sizem_for_pyramid_subblocks = true;
    reader->Open(stream, &open_options);

    // create the stream-object representing the destination file
    const std::string output_string(output_path);
    const auto output_stream = libCZI::CreateOutputStreamForFile(utils::utf8::WidenUtf8(output_string).c_str(), false);

    // create (and configure) the "CZI-writer"-object - it is configured to ignore "duplicate subblocks"
    libCZI::CZIWriterOptions czi_writer_options;
    czi_writer_options.allow_duplicate_subblocks = true;
    const auto writer = libCZI::CreateCZIWriter(&czi_writer_options);

    // GUID_NULL here means that a new Guid is created
    const auto czi_writer_info = std::make_shared<libCZI::CCziWriterInfo>(libCZI::GUID{0, 0, 0, {0, 0, 0, 0, 0, 0, 0, 0}});

    // TODO(JBL): it might be desirable to make reservations for the
    // subblock-directory-/attachments-directory-/metadata-segment
    //            (so that the end up at the beginning of the file instead
    //            of at the end), but this shouldn't make a difference in
    //            terms of proper operation
    writer->Create(output_stream, czi_writer_info);

    auto operation = CreateOperationUp();
    OperationDescription operation_description;
    operation_description.reader = reader;
    operation_description.writer = writer;
    operation_description.command = this->command_;
    operation_description.compression_strategy = this->compression_strategy_;
    operation_description.compression_option = FileProcessor::CreateCompressionOptions(this->compression_level_);
    operation->SetParameters(operation_description);

    operation->DoOperation(
        [&progress_report](const ProgressInfo &progress_info) -> bool
        {
          // The task here is to map the "progress_info" to a value between 0 and 100
          // What we do is:
          // We assign those weights to the phases:
          // CopySubblocks: 95%
          // CopyAttachments: 4%
          // WriteXmlMetadata: 1%
          //
          // and we put in the assumption that the phases occur in exactly this order.
          float phase_progress = 50;
          if (progress_info.phase == ProcessingPhase::kCopySubblocks && progress_info.number_of_items_todo > 0)
          {
            phase_progress =
                static_cast<float>(progress_info.number_of_items_done) / static_cast<float>(progress_info.number_of_items_todo);
          }

          float total_progress = 0;
          switch (progress_info.phase)
          {
            case ProcessingPhase::kCopySubblocks:
              total_progress = 95 * phase_progress;
              break;
            case ProcessingPhase::kCopyAttachments:
              total_progress = 95 + 4 * phase_progress;
              break;
            case ProcessingPhase::kWriteXmlMetadata:
              total_progress = 95 + 4 + 1 * phase_progress;
          }

          return progress_report((int32_t)total_progress);
        });

    writer->Close();
    reader->Close();
  }

private:
  static libCZI::Utils::CompressionOption CreateCompressionOptions(int compression_level)
  {
    std::stringstream ss;
    ss << "zstd1:ExplicitLevel=" << compression_level << ";"
       << "PreProcess=HiLoByteUnpack" << std::endl;
    return libCZI::Utils::ParseCompressionOptions(ss.str());
  }
};

void *CreateFileProcessor(Command command, CompressionStrategy strategy, int compression_level)
{
  return new FileProcessor(command, strategy, compression_level);
}

void DestroyFileProcessor(void *file_processor)
{
  auto *processor = static_cast<FileProcessor *>(file_processor);
  delete processor;
}

void GetLibVersion(int32_t *major, int32_t *minor, int32_t *patch)
{
  *major = atoi(CZICOMPRESS_VERSION_MAJOR);
  *minor = atoi(CZICOMPRESS_VERSION_MINOR);
  *patch = atoi(CZICOMPRESS_VERSION_PATCH);
}

bool GetLibVersionString(char *buffer, uint64_t *size)
{
  std::ostringstream version_stream;
  const std::string version_major(CZICOMPRESS_VERSION_MAJOR);
  const std::string version_minor(CZICOMPRESS_VERSION_MINOR);
  const std::string version_patch(CZICOMPRESS_VERSION_PATCH_STR);
  const std::string version_tweak(CZICOMPRESS_VERSION_TWEAK_STR);

  version_stream << version_major << "." << version_minor << "." << version_patch;
  if (!version_tweak.empty())
  {
    version_stream << "." << version_tweak;
  }

  const auto composed_version_string = version_stream.str();

  const size_t required_buffer_size = composed_version_string.size() + 1;  // add 1 for the terminating '\0'

  if (required_buffer_size > *size)
  {
    *size = required_buffer_size;
    return false;
  }

  memcpy(buffer, composed_version_string.c_str(), required_buffer_size);

  return true;
}

int ProcessFile(void *file_processor, const char *const input_path, const char *const output_path, char *error_message,
                size_t *error_message_length, ProgressReport progress)
{
  try
  {
    if (!file_processor) throw std::invalid_argument("File Processor pointer is null.");
    if (!input_path) throw std::invalid_argument("Input Path pointer is null.");
    if (!output_path) throw std::invalid_argument("Output path pointer is null.");
    if (!error_message) throw std::invalid_argument("Error message pointer is null.");
    if (!error_message_length) throw std::invalid_argument("Error message length pointer is null.");
    if (!progress) throw std::invalid_argument("Progress function pointer is null");

    auto *proc = static_cast<FileProcessor *>(file_processor);
    proc->ProcessFile(input_path, output_path, progress);
    error_message = nullptr;
    return EXIT_SUCCESS;
  }
  catch (const std::exception &exception)
  {
    const auto *messagestr = exception.what();
    const size_t len = strlen(messagestr);
    
    // Pointer/s for passing back error information are/is invalid so we do not attempt to write.
    if (!error_message || !error_message_length)
    {
      return EXIT_FAILURE;
    }
    
    if (len < *error_message_length)
    {
      *error_message_length = len;
    }

    // Checking for null pointers only not necessarily for invalid string;
    if (!messagestr)
    {
      std::string error_error_message("Error encountered while parsing error message");
      *error_message_length = error_error_message.size() + 1;  // the addition of one is on account of the null terminator char;
      strncpy(error_message, error_error_message.c_str(), *error_message_length);
      return EXIT_FAILURE;
    }

    // TODO(DKU): Encode error_message as UTF8
    strncpy(error_message, messagestr, *error_message_length);
    return EXIT_FAILURE;
  }
}
