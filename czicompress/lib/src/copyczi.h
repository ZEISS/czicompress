// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once

#include <cstdint>
#include <functional>
#include <memory>
#include <tuple>
#include <utility>

#include "../inc_libCZI.h"
#include "../include/compressionstrategy.h"
#include "../include/progressinfo.h"
#include "actionwithsubblockstatistics.h"

/// This abstract base class is implementing the following functionality:
/// - We run through all subblocks of the source document.
/// - For each subblock we call the (abstract) method
/// 'DecideWhatToDoWithSubBlock' to decide what to do with it.
/// - For the action "Compress" we call the (abstract) method 'CompressSubBlock'
/// to compress the subblock, the other actions are implemented in this base
/// class.
/// - After we are done with the subblocks, we then copy the attachments and
/// metadata segment.
class CopyCziBase
{
public:
  /// @brief Creates a CopyCziBase object.
  /// @param reader The reader used for reading.
  /// @param writer The writer used for writing.
  /// @param progress_report The progress reporting function.
  CopyCziBase(std::shared_ptr<libCZI::ICZIReader> reader, std::shared_ptr<libCZI::ICziWriter> writer,
              std::function<bool(const ProgressInfo&)> progress_report);

  virtual ~CopyCziBase() = default;

  /// Run the operation. In case of an error, an exception is thrown. The
  /// 'progress_report' function is called periodically (after an item has
  /// finished) to report the progress. The function should return 'true' to
  /// continue operation. In case it returns false, operation is aborted and
  /// 'Run' returns false.
  ///
  /// \returns    True if operation completed successfully; false if operation
  /// was aborted.
  bool Run();

protected:
  /// Gets the statistics object.
  ///
  /// \returns The statistics object.
  const ActionWithSubBlockStatistics& GetStatistics() const;

  /// Values that represent the action to be taken with a subblock.
  enum class ActionWithSubBlock
  {
    kCopy,        ///< The subblock is to be copied verbatim.
    kDecompress,  ///< The subblock is to be decompressed and then written.
    kCompress,    ///< The subblock is to be compressed (and, if necessary,
                  ///< decompressed before) and then written.
  };

  /// This method is called for each subblock, and it is to be determined which
  /// action to take place with the subblock. This method is abstract, and must
  /// be implemented by the derived class.
  ///
  /// \param  subblock    The subblock.
  ///
  /// \returns    An enum specifying the action to take place with the subblock.
  virtual ActionWithSubBlock DecideWhatToDoWithSubBlock(const std::shared_ptr<libCZI::ISubBlock>& subblock) = 0;

  /// In case the action is "Compress", then this method is called to compress
  /// the subblock. This method is only called for the action "Compress", and
  /// the base class implementation throws an exception.
  ///
  /// \param  subblock    The subblock.
  ///
  /// \returns    A
  /// std::tuple&lt;libCZI::CompressionMode,std::shared_ptr&lt;libCZI::IMemoryBlock&gt;&gt;
  /// containing the compression mode and the compressed data.
  virtual std::tuple<libCZI::CompressionMode, std::shared_ptr<libCZI::IMemoryBlock>> CompressSubBlock(
      const std::shared_ptr<libCZI::ISubBlock>& subblock);

  /// This method is called when the metadata-segment is being process. It allows to
  /// alter the XML and do modifications.
  /// If no modification is needed (and the data from the source metadata-segment) is to be
  /// copied verbatim, then this method can return a nullptr.
  /// This is what the base-class implementation does.
  /// Note that the metadata-segment is written last, i.e. after all subblocks have been processed
  /// (so the subblock-action-statistics is valid by then).
  ///
  /// \param  metadata_segment The metadata segment (of the source document).
  ///
  /// \returns A std::shared_ptr&lt;libCZI::ICziMetadataBuilder&gt; containing the
  ///          metadata to be written to the destination document.
  virtual std::shared_ptr<libCZI::ICziMetadataBuilder> ModifyMetadata(const std::shared_ptr<libCZI::IMetadataSegment>& metadata_segment);

private:
  /// This object is used to keep a statistics about the operations done with the subblocks.
  ActionWithSubBlockStatistics action_count;

  static int CheckUint32AndCastToInt(uint32_t value);

  /// This utility is copying all relevant information from the source subblock 'subblock'
  /// to the target data structure 'add_subblock_info_target', i.e. the subblock's coordinate,
  /// its logical and physical position and so on. So, it fills out the information required
  /// for writing the target subblock except that data itself and also except compression-related
  /// information.
  ///
  /// \param          subblock                 The source subblock.
  /// \param [out]    add_subblock_info_target The data structure describing the to-be-written subblock.
  static void SetPositionCoordinatePixelType(const std::shared_ptr<libCZI::ISubBlock>& subblock,
                                             libCZI::AddSubBlockInfoBase& add_subblock_info_target);

  void ProcessAndWriteSubBlock(const std::shared_ptr<libCZI::ISubBlock>& subblock);
  void WriteAttachment(const std::shared_ptr<libCZI::IAttachment>& attachment);
  void WriteMetadataSegment(const std::shared_ptr<libCZI::IMetadataSegment>& metadata_segment);

  void WriteSubblockVerbatim(const std::shared_ptr<libCZI::ISubBlock>& subblock);
  void DecompressAndWriteSubBlock(const std::shared_ptr<libCZI::ISubBlock>& subblock);
  void CompressAndWriteSubBlock(const std::shared_ptr<libCZI::ISubBlock>& subblock);

  std::shared_ptr<libCZI::ICZIReader> reader_;
  std::shared_ptr<libCZI::ICziWriter> writer_;
  std::function<bool(const ProgressInfo&)> progress_report_;
};

/// Implementation of the "copy operation" which compresses the output The
/// decision whether to compress is based on the specified compression strategy.
class CopyCziAndCompress : public CopyCziBase
{
private:
  CompressionStrategy strategy_{CompressionStrategy::kInvalid};
  libCZI::Utils::CompressionOption compression_option_;

public:
  CopyCziAndCompress(std::shared_ptr<libCZI::ICZIReader> reader, std::shared_ptr<libCZI::ICziWriter> writer,
                     std::function<bool(const ProgressInfo&)> progress_report, CompressionStrategy strategy,
                     libCZI::Utils::CompressionOption compression_option)
      : CopyCziBase(std::move(reader), std::move(writer), progress_report),
        strategy_(strategy),
        compression_option_(std::move(compression_option))
  {
  }

protected:
  ActionWithSubBlock DecideWhatToDoWithSubBlock(const std::shared_ptr<libCZI::ISubBlock>& subblock) override;
  std::shared_ptr<libCZI::ICziMetadataBuilder> ModifyMetadata(const std::shared_ptr<libCZI::IMetadataSegment>& metadata_segment) override;
  std::tuple<libCZI::CompressionMode, std::shared_ptr<libCZI::IMemoryBlock>> CompressSubBlock(
      const std::shared_ptr<libCZI::ISubBlock>& subblock) override;
};

/// Implementation of the "copy operation" which decompresses the output.
class CopyCziAndDecompress : public CopyCziBase
{
public:
  CopyCziAndDecompress(std::shared_ptr<libCZI::ICZIReader> reader, std::shared_ptr<libCZI::ICziWriter> writer,
                       std::function<bool(const ProgressInfo&)> progress_report)
      : CopyCziBase(std::move(reader), std::move(writer), std::move(progress_report))
  {
  }

protected:
  ActionWithSubBlock DecideWhatToDoWithSubBlock(const std::shared_ptr<libCZI::ISubBlock>& subblock) override;
  std::shared_ptr<libCZI::ICziMetadataBuilder> ModifyMetadata(const std::shared_ptr<libCZI::IMetadataSegment>& metadata_segment) override;
};
