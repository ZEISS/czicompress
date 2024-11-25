// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#include "copyczi.h"

#include <limits>
#include <memory>
#include <tuple>
#include <utility>

namespace
{  // unnamed namespace makes functions only accessible from this file

std::uint32_t CheckSizeAndCastToUint32(const size_t size)
{
  if (size > std::numeric_limits<uint32_t>::max())
  {
    throw std::overflow_error("Error: size too large: " + std::to_string(size));
  }

  return static_cast<uint32_t>(size);
}

template <typename AddSubBlockInfoType>
void InitMetaDataAndAttachment(AddSubBlockInfoType& subblock_info_target, const std::shared_ptr<libCZI::ISubBlock>& subblock)
{
  subblock_info_target.Clear();
  size_t metadata_size = 0;
  const std::shared_ptr<const void> spMetadata = subblock->GetRawData(libCZI::ISubBlock::MemBlkType::Metadata, &metadata_size);
  subblock_info_target.ptrSbBlkMetadata = spMetadata.get();
  subblock_info_target.sbBlkMetadataSize = CheckSizeAndCastToUint32(metadata_size);

  size_t attachment_size = 0;
  const std::shared_ptr<const void> spAttachment = subblock->GetRawData(libCZI::ISubBlock::MemBlkType::Attachment, &attachment_size);
  subblock_info_target.ptrSbBlkAttachment = spAttachment.get();
  subblock_info_target.sbBlkAttachmentSize = CheckSizeAndCastToUint32(attachment_size);
}

}  // namespace

CopyCziBase::CopyCziBase(std::shared_ptr<libCZI::ICZIReader> reader, std::shared_ptr<libCZI::ICziWriter> writer,
                         std::function<bool(const ProgressInfo&)> progress_report)
    : reader_(std::move(reader)), writer_(std::move(writer)), progress_report_(std::move(progress_report))
{
}

int CopyCziBase::CheckUint32AndCastToInt(const uint32_t value)
{
  if (value > static_cast<uint32_t>(std::numeric_limits<int>::max()))
  {
    throw std::overflow_error("Error: value too large: " + std::to_string(value));
  }

  return static_cast<int>(value);
}

const ActionWithSubBlockStatistics& CopyCziBase::GetStatistics() const { return this->action_count; }

/*static*/ void CopyCziBase::SetPositionCoordinatePixelType(const std::shared_ptr<libCZI::ISubBlock>& subblock,
                                                            libCZI::AddSubBlockInfoBase& add_subblock_info_target)
{
  const libCZI::SubBlockInfo subblock_info_source = subblock->GetSubBlockInfo();
  add_subblock_info_target.coordinate = subblock_info_source.coordinate;
  add_subblock_info_target.x = subblock_info_source.logicalRect.x;
  add_subblock_info_target.y = subblock_info_source.logicalRect.y;
  add_subblock_info_target.logicalWidth = subblock_info_source.logicalRect.w;
  add_subblock_info_target.logicalHeight = subblock_info_source.logicalRect.h;
  add_subblock_info_target.physicalWidth = CopyCziBase::CheckUint32AndCastToInt(subblock_info_source.physicalSize.w);
  add_subblock_info_target.physicalHeight = CopyCziBase::CheckUint32AndCastToInt(subblock_info_source.physicalSize.h);
  add_subblock_info_target.PixelType = subblock_info_source.pixelType;
  add_subblock_info_target.pyramid_type = subblock_info_source.pyramidType;

  if (subblock_info_source.physicalSize.w == subblock_info_source.logicalRect.w &&
      subblock_info_source.physicalSize.h == subblock_info_source.logicalRect.h &&
      subblock_info_source.mIndex != (std::numeric_limits<int>::max)())
  {
    add_subblock_info_target.mIndex = subblock_info_source.mIndex;
    add_subblock_info_target.mIndexValid = true;
  }
  else
  {
    add_subblock_info_target.mIndex = 0;
    add_subblock_info_target.mIndexValid = false;
  }
}

bool CopyCziBase::Run()
{
  ProgressInfo progress_info;
  progress_info.phase = ProcessingPhase::kCopySubblocks;
  progress_info.number_of_items_todo = this->reader_->GetStatistics().subBlockCount;

  bool was_cancelled = false;

  this->reader_->EnumerateSubBlocks(
      [&](int index, const libCZI::SubBlockInfo&) -> bool
      {
        const auto subblock = this->reader_->ReadSubBlock(index);
        this->ProcessAndWriteSubBlock(subblock);
        progress_info.number_of_items_done++;
        if (this->progress_report_ && !this->progress_report_(progress_info))
        {
          was_cancelled = true;
          return false;
        }

        return true;
      });

  if (was_cancelled)
  {
    return false;
  }

  progress_info = ProgressInfo();
  progress_info.phase = ProcessingPhase::kCopyAttachments;
  // TODO(JBL): Currently, there is no (easy) way to find the total number of
  // attachments (afaik).
  //             So, we set the number of items to do to -1, which means
  //             "unknown".
  progress_info.number_of_items_todo = -1;
  this->reader_->EnumerateAttachments(
      [&](int index, const libCZI::AttachmentInfo&) -> bool
      {
        const auto attachment = this->reader_->ReadAttachment(index);
        this->WriteAttachment(attachment);
        ++progress_info.number_of_items_done;
        if (this->progress_report_ && !this->progress_report_(progress_info))
        {
          was_cancelled = true;
          return false;
        }

        return true;
      });

  if (was_cancelled)
  {
    return false;
  }

  progress_info = ProgressInfo();
  progress_info.phase = ProcessingPhase::kWriteXmlMetadata;
  progress_info.number_of_items_todo = 1;
  if (this->progress_report_ && !this->progress_report_(progress_info))
  {
    return false;
  }

  const auto metadata_segment = this->reader_->ReadMetadataSegment();
  this->WriteMetadataSegment(metadata_segment);

  progress_info.number_of_items_done = 1;
  auto failed = this->progress_report_ && !this->progress_report_(progress_info);
  return !failed;
}

void CopyCziBase::ProcessAndWriteSubBlock(const std::shared_ptr<libCZI::ISubBlock>& subblock)
{
  auto action_with_subblock = this->DecideWhatToDoWithSubBlock(subblock);
  switch (action_with_subblock)
  {
    case ActionWithSubBlock::kCopy:
      this->WriteSubblockVerbatim(subblock);
      break;
    case ActionWithSubBlock::kDecompress:
      this->DecompressAndWriteSubBlock(subblock);
      break;
    case ActionWithSubBlock::kCompress:
      this->CompressAndWriteSubBlock(subblock);
      break;
  }
}

void CopyCziBase::WriteAttachment(const std::shared_ptr<libCZI::IAttachment>& attachment)
{
  auto guid = attachment->GetAttachmentInfo().contentGuid;

  const void* ptr{nullptr};
  size_t sizeData = 0;
  attachment->DangerousGetRawData(ptr, sizeData);
  auto dataSize = CheckSizeAndCastToUint32(sizeData);

  libCZI::AddAttachmentInfo add_attachment_info{guid, {}, {}, ptr, dataSize};
  add_attachment_info.SetContentFileType(attachment->GetAttachmentInfo().contentFileType);  // NOLINT
  add_attachment_info.SetName(attachment->GetAttachmentInfo().name.c_str());

  this->writer_->SyncAddAttachment(add_attachment_info);
}

void CopyCziBase::WriteMetadataSegment(const std::shared_ptr<libCZI::IMetadataSegment>& metadata_segment)
{
  libCZI::WriteMetadataInfo write_metadata_info;
  write_metadata_info.Clear();

  // allow a derived class to modify the XML-metadata
  const auto altered_metadata_builder = this->ModifyMetadata(metadata_segment);
  if (!altered_metadata_builder)
  {
    // we want to write a verbatim copy of the source document's metadata
    const void* ptr{nullptr};
    size_t size = 0;
    metadata_segment->DangerousGetRawData(libCZI::IMetadataSegment::MemBlkType::XmlMetadata, ptr, size);
    write_metadata_info.szMetadata = static_cast<const char*>(ptr);
    write_metadata_info.szMetadataSize = size;

    this->writer_->SyncWriteMetadata(write_metadata_info);
  }
  else
  {
    // we are provided with a metadata-object which we should use (potentially modified), so
    //  let's use this
    const auto altered_metadata_xml = altered_metadata_builder->GetXml();
    write_metadata_info.szMetadata = altered_metadata_xml.c_str();
    write_metadata_info.szMetadataSize = altered_metadata_xml.size();

    // Note: We need to be careful (since we have to pass a naked pointer to the "SyncWriteMetadata"
    //       method) that the data we point to (i.e. the string) is valid during this call. So,
    //       the string object (altered_metadata_xml) must not get out-of-scope before we return
    //       from "SyncWriteMetadata".
    this->writer_->SyncWriteMetadata(write_metadata_info);
  }
}

void CopyCziBase::WriteSubblockVerbatim(const std::shared_ptr<libCZI::ISubBlock>& subblock)
{
  libCZI::AddSubBlockInfoMemPtr subblock_info_target;

  InitMetaDataAndAttachment(subblock_info_target, subblock);

  size_t size_data = 0;
  const std::shared_ptr<const void> rawData = subblock->GetRawData(libCZI::ISubBlock::MemBlkType::Data, &size_data);
  subblock_info_target.ptrData = rawData.get();
  subblock_info_target.dataSize = CheckSizeAndCastToUint32(size_data);

  CopyCziBase::SetPositionCoordinatePixelType(subblock, subblock_info_target);
  subblock_info_target.compressionModeRaw = subblock->GetSubBlockInfo().compressionModeRaw;

  this->writer_->SyncAddSubBlock(subblock_info_target);
  this->action_count.Increment_CopiedVerbatim();
}

void CopyCziBase::DecompressAndWriteSubBlock(const std::shared_ptr<libCZI::ISubBlock>& subblock)
{
  const libCZI::CompressionMode subblock_compression_mode = subblock->GetSubBlockInfo().GetCompressionMode();
  switch (subblock_compression_mode)
  {
    default:
    case libCZI::CompressionMode::UnCompressed:
      // well, in this case we have nothing other to do than to copy the
      // subblock verbatim
      this->WriteSubblockVerbatim(subblock);
      break;
    case libCZI::CompressionMode::JpgXr:
    case libCZI::CompressionMode::Zstd0:
    case libCZI::CompressionMode::Zstd1:
    {
      libCZI::AddSubBlockInfoStridedBitmap subblock_info_target;

      InitMetaDataAndAttachment(subblock_info_target, subblock);

      const auto bitmap = subblock->CreateBitmap();
      const libCZI::ScopedBitmapLockerSP bitmap_locked{bitmap};
      subblock_info_target.ptrBitmap = bitmap_locked.ptrDataRoi;
      subblock_info_target.strideBitmap = bitmap_locked.stride;

      CopyCziBase::SetPositionCoordinatePixelType(subblock, subblock_info_target);
      subblock_info_target.SetCompressionMode(libCZI::CompressionMode::UnCompressed);  // set uncompressed.

      this->writer_->SyncAddSubBlock(subblock_info_target);
      this->action_count.Increment_Decompressed();
    }

    break;
  }
}

void CopyCziBase::CompressAndWriteSubBlock(const std::shared_ptr<libCZI::ISubBlock>& subblock)
{
  // First, we check if we can decompress the subblock (or - if it is already
  // uncompressed) - If not, we cannot compress it obviously, and what we do is
  // to copy it verbatim then
  const libCZI::CompressionMode subblock_compression_mode = subblock->GetSubBlockInfo().GetCompressionMode();
  if (subblock_compression_mode != libCZI::CompressionMode::UnCompressed && subblock_compression_mode != libCZI::CompressionMode::JpgXr &&
      subblock_compression_mode != libCZI::CompressionMode::Zstd0 && subblock_compression_mode != libCZI::CompressionMode::Zstd1)
  {
    this->WriteSubblockVerbatim(subblock);
  }
  else
  {
    // If that's not the case - then let's get the bitmap, compress it, and
    // write it out
    libCZI::AddSubBlockInfoMemPtr subblock_info_target;
    InitMetaDataAndAttachment(subblock_info_target, subblock);

    const auto compression_mode_and_compressed_memory_block = this->CompressSubBlock(subblock);

    subblock_info_target.ptrData = std::get<1>(compression_mode_and_compressed_memory_block)->GetPtr();
    subblock_info_target.dataSize = CheckSizeAndCastToUint32(std::get<1>(compression_mode_and_compressed_memory_block)->GetSizeOfData());

    CopyCziBase::SetPositionCoordinatePixelType(subblock, subblock_info_target);
    subblock_info_target.SetCompressionMode(std::get<0>(compression_mode_and_compressed_memory_block) /*CompressionMode::Zstd1*/);
    this->writer_->SyncAddSubBlock(subblock_info_target);
    this->action_count.Increment_Compressed();
  }
}

std::tuple<libCZI::CompressionMode, std::shared_ptr<libCZI::IMemoryBlock>> CopyCziBase::CompressSubBlock(
    const std::shared_ptr<libCZI::ISubBlock>&)
{
  throw std::runtime_error("The method or operation is not implemented.");
}

std::shared_ptr<libCZI::ICziMetadataBuilder> CopyCziBase::ModifyMetadata(const std::shared_ptr<libCZI::IMetadataSegment>& metadata_segment)
{
  // base class implementation does nothing here
  return nullptr;
}

//-----------------------------------------------------------------------------

CopyCziAndCompress::ActionWithSubBlock CopyCziAndCompress::DecideWhatToDoWithSubBlock(const std::shared_ptr<libCZI::ISubBlock>& subblock)
{
  switch (this->strategy_)
  {
    case CompressionStrategy::kAll:
      return ActionWithSubBlock::kCompress;
    case CompressionStrategy::kOnlyUncompressed:
      if (subblock->GetSubBlockInfo().GetCompressionMode() == libCZI::CompressionMode::UnCompressed)
      {
        return ActionWithSubBlock::kCompress;
      }

      return ActionWithSubBlock::kCopy;
    case CompressionStrategy::kUncompressedAndZStdCompressed:
    {
      const libCZI::CompressionMode subblock_compression_mode = subblock->GetSubBlockInfo().GetCompressionMode();
      if (subblock_compression_mode == libCZI::CompressionMode::UnCompressed ||
          subblock_compression_mode == libCZI::CompressionMode::Zstd0 || subblock_compression_mode == libCZI::CompressionMode::Zstd1)
      {
        return ActionWithSubBlock::kCompress;
      }

      return ActionWithSubBlock::kCopy;
    }
    default:
      throw std::runtime_error("Unknown strategy");
  }
}

std::tuple<libCZI::CompressionMode, std::shared_ptr<libCZI::IMemoryBlock>> CopyCziAndCompress::CompressSubBlock(
    const std::shared_ptr<libCZI::ISubBlock>& subblock)
{
  if (this->compression_option_.first != libCZI::CompressionMode::Zstd0 &&
      this->compression_option_.first != libCZI::CompressionMode::Zstd1)
  {
    throw std::runtime_error("Unknown or unsupported compression mode");
  }

  const auto bitmap = subblock->CreateBitmap();
  const libCZI::ScopedBitmapLockerSP bitmap_locked(bitmap);

  const auto compressed_memory_block =
      this->compression_option_.first == libCZI::CompressionMode::Zstd0
          ? libCZI::ZstdCompress::CompressZStd0Alloc(bitmap->GetWidth(), bitmap->GetHeight(), bitmap_locked.stride, bitmap->GetPixelType(),
                                                     bitmap_locked.ptrDataRoi, this->compression_option_.second.get())
          : libCZI::ZstdCompress::CompressZStd1Alloc(bitmap->GetWidth(), bitmap->GetHeight(), bitmap_locked.stride, bitmap->GetPixelType(),
                                                     bitmap_locked.ptrDataRoi, this->compression_option_.second.get());
  return std::make_tuple(this->compression_option_.first, compressed_memory_block);
}

std::shared_ptr<libCZI::ICziMetadataBuilder> CopyCziAndCompress::ModifyMetadata(
    const std::shared_ptr<libCZI::IMetadataSegment>& metadata_segment)
{
  // well... if we at least compressed half of the subblocks, then we feel entitled to set the
  // documents metadata (which states the "prevalant compression")
  if (this->GetStatistics().GetCountOfSubblocksCompressed() >= this->GetStatistics().GetTotalCountOfSubblocksProcessed() / 2)
  {
    auto metadata_src = metadata_segment->CreateMetaFromMetadataSegment();
    const auto metadata_builder = libCZI::CreateMetadataBuilderFromXml(metadata_src->GetXml());

    metadata_builder->GetRootNode()
        ->GetOrCreateChildNode("Metadata/Information/Image/CurrentCompressionParameters")
        ->SetValue("Lossless: True");

    return metadata_builder;
  }

  return nullptr;
}

//-----------------------------------------------------------------------------

CopyCziAndDecompress::ActionWithSubBlock CopyCziAndDecompress::DecideWhatToDoWithSubBlock(const std::shared_ptr<libCZI::ISubBlock>&)
{
  return ActionWithSubBlock::kDecompress;
}

std::shared_ptr<libCZI::ICziMetadataBuilder> CopyCziAndDecompress::ModifyMetadata(
    const std::shared_ptr<libCZI::IMetadataSegment>& metadata_segment)
{
  // well... if we at least decompressed half of the subblocks, then we feel entitled to set the
  // documents metadata (which states the "prevalant compression") - in this case, we set the
  // "OriginalCompressionMethod"/"OriginalEncodingQuality" elements to
  // the appropriate values (i.e. "Uncompressed").
  if (this->GetStatistics().GetCountOfSubblocksDecompressed() >= this->GetStatistics().GetTotalCountOfSubblocksProcessed() / 2)
  {
    auto metadata_src = metadata_segment->CreateMetaFromMetadataSegment();
    const auto metadata_builder = libCZI::CreateMetadataBuilderFromXml(metadata_src->GetXml());

    metadata_builder->GetRootNode()->GetOrCreateChildNode("Metadata/Information/Image/CurrentCompressionParameters")->SetValue("");

    return metadata_builder;
  }

  return nullptr;
}
