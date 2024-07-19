// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once

#include <cstdint>

/// This class is providing statistics about a "copy-operation" - how many
/// subblocks were compressed, uncompressed or copied verbatim.
class ActionWithSubBlockStatistics
{
private:
  std::uint64_t count_subblocks_copied_verbatim_{0};
  std::uint64_t count_subblocks_compressed_{0};
  std::uint64_t count_subblocks_decompressed_{0};

public:
  /// Default constructor for ActionWithSubBlockStatistics class.
  ActionWithSubBlockStatistics() = default;

  /// Copy constructor and move constructor deleted.
  ActionWithSubBlockStatistics(const ActionWithSubBlockStatistics&) = delete;
  ActionWithSubBlockStatistics(ActionWithSubBlockStatistics&&) = delete;

  /// Assignment operator deleted for ActionWithSubBlockStatistics object.
  ActionWithSubBlockStatistics& operator=(const ActionWithSubBlockStatistics& rhs) = delete;

  /// Increment the count of subblocks copied verbatim.
  void Increment_CopiedVerbatim() { ++this->count_subblocks_copied_verbatim_; }

  /// Increment the count of subblocks compressed.
  void Increment_Compressed() { ++this->count_subblocks_compressed_; }

  /// Increment the count of subblocks decompressed.
  void Increment_Decompressed() { ++this->count_subblocks_decompressed_; }

  /// Get the count of subblocks copied verbatim.
  /// \returns Count of subblocks copied verbatim.
  std::uint64_t GetCountOfSubblocksCopiedVerbatim() const { return this->count_subblocks_copied_verbatim_; }

  /// Get the count of subblocks compressed.
  /// \returns Count of subblocks compressed.
  std::uint64_t GetCountOfSubblocksCompressed() const { return this->count_subblocks_compressed_; }

  /// Get the count of subblocks decompressed.
  /// \returns Count of subblocks decompressed.
  std::uint64_t GetCountOfSubblocksDecompressed() const { return this->count_subblocks_decompressed_; }

  /// Get the total count of subblocks processed.
  /// \returns Total count of subblocks processed.
  std::uint64_t GetTotalCountOfSubblocksProcessed() const
  {
    return this->count_subblocks_copied_verbatim_ + this->count_subblocks_compressed_ + this->count_subblocks_decompressed_;
  }
};
