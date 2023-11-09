// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once

#include <memory>

#include "copyczi.h"
#include "inc_libCZI.h"
#include "include/IOperation.h"
#include "include/compressionstrategy.h"

class Operation final : public IOperation
{
private:
  OperationDescription description_;

public:
  ~Operation() override = default;

  void SetParameters(const OperationDescription& description) override;
  void DoOperation(const std::function<bool(const ProgressInfo&)>& progress) override;

private:
  std::unique_ptr<CopyCziBase> CreateCopyClass(const std::function<bool(const ProgressInfo&)>& progress);
};
