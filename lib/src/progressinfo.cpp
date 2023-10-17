// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#include "../include/progressinfo.h"

const char* ProcessingPhaseAsInformalString(ProcessingPhase phase)
{
  switch (phase)
  {
    case ProcessingPhase::kCopySubblocks:
      return "Subblocks";
    case ProcessingPhase::kCopyAttachments:
      return "Attachments";
    case ProcessingPhase::kWriteXmlMetadata:
      return "XmlMetadata";
    default:
      return "Unknown";
  }
}
