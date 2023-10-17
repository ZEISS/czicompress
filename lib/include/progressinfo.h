// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once

/// Values that represent distinct phases in the CZI-copy operation.
enum class ProcessingPhase
{
  /// An enum constant representing the invalid option.
  kInvalid,

  /// An enum constant representing the phase where subblocks are processed.
  kCopySubblocks,

  /// An enum constant representing the phase where attachments are processed.
  kCopyAttachments,

  /// An enum constant representing the phase where the "XML metadata segment"
  /// is processed.
  kWriteXmlMetadata,
};

/// Information about the progress in the process of copy operation
struct ProgressInfo
{
  // The current processing phase.
  ProcessingPhase phase{ProcessingPhase::kInvalid};

  // Number of items processed in the current phase, initially 0.
  int number_of_items_done{0};

  /// If greater or equal to zero, gives the items to process in current phase.
  /// If less than zero, then no information about the number of items to
  /// process is available.
  int number_of_items_todo{0};
};

/// Convert the enum to an informal string.
/// \param  phase   The phase.
/// \returns    A static string constant with an informal description of the
///             phase.
const char* ProcessingPhaseAsInformalString(ProcessingPhase phase);
