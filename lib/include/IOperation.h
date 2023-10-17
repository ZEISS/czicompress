// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once

#include <memory>

#include "command.h"
#include "compressionstrategy.h"
#include "inc_libCZI.h"
#include "progressinfo.h"

/// This struct gathers all the information needed to perform a copy operation.
/// Note that "copy operation" here is meant to be a generic term, something
/// like "run through all parts of the source document, potentially transform
/// them, and write out the result". It is not necessarily a 1:1 copy of the
/// source document in our case of course.
struct OperationDescription
{
  /// The reader object - representing the source document.
  std::shared_ptr<libCZI::ICZIReader> reader;

  /// The writer object - representing the destination document.
  std::shared_ptr<libCZI::ICziWriter> writer;

  /// The command or operation that is to be performed.
  Command command{Command::kInvalid};

  /// (only valid in case of 'compress' command) The compression strategy, how
  /// to decide which subblocks to operate on.
  CompressionStrategy compression_strategy{CompressionStrategy::kInvalid};

  /// (only valid in case of 'compress' command) The compression option.
  libCZI::Utils::CompressionOption compression_option;
};

/// This interface encapsulates all functionality for a transform operation
/// on a source reader object and a destionation writer object.
class IOperation
{
public:
  /// Sets the parameters controlling the operation and the objects on which
  /// the operation takes place.
  /// Note that when passing in the parameters, this class will keep a reference
  /// on the reader and writer object. Those references are guaranteed to be released
  /// when the instance is destroyed.
  ///
  /// \param  description The description.
  virtual void SetParameters(const OperationDescription& description) = 0;

  /// Executes the operation synchronously. In case of errors during the operation,
  /// an exception is thrown.
  ///
  /// \param  progress The progress function which will be called periodically. If the
  ///                  functor returns false, the operation will be cancelled.
  virtual void DoOperation(const std::function<bool(const ProgressInfo&)>& progress) = 0;

  virtual ~IOperation() = default;
};

/// Constructs a shared pointer containing a new instance of the IOperation object.
///
/// \returns The newly constructed IOperation object.
std::shared_ptr<IOperation> CreateOperationSp();

/// Constructs an unique pointer containing a new instance of the IOperation object.
///
/// \returns The newly constructed IOperation object.
std::unique_ptr<IOperation> CreateOperationUp();
