// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#include "operation.h"

#include <memory>

#include "copyczi.h"
#include "inc_libCZI.h"

std::shared_ptr<IOperation> CreateOperationSp() { return std::make_shared<Operation>(); }

std::unique_ptr<IOperation> CreateOperationUp() { return std::make_unique<Operation>(); }

void Operation::SetParameters(const OperationDescription& description) { this->description_ = description; }

void Operation::DoOperation(const std::function<bool(const ProgressInfo&)>& progress)
{
  std::function<bool(const ProgressInfo&)> progress_report_function;
  if (progress)
  {
    progress_report_function = [progress](const ProgressInfo& info) -> bool
    {
      return progress(info);
    };
  }

  const auto operation = this->CreateCopyClass(progress_report_function);
  operation->Run();
}

std::unique_ptr<CopyCziBase> Operation::CreateCopyClass(const std::function<bool(const ProgressInfo&)>& progress)
{
  switch (this->description_.command)
  {
    case Command::kDecompress:
      return std::make_unique<CopyCziAndDecompress>(this->description_.reader, this->description_.writer, progress);
    case Command::kCompress:
      return std::make_unique<CopyCziAndCompress>(this->description_.reader, this->description_.writer, progress,
                                                  this->description_.compression_strategy, this->description_.compression_option);
    default:
      throw std::runtime_error("Unknown command");
  }
}
