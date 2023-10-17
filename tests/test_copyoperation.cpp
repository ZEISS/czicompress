// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#include <src/copyczi.h>

#include <catch2/catch_test_macros.hpp>
#include <memory>
#include <tuple>

#include "catch2/catch_all.hpp"
#include "libczi_utils.h"

using std::make_shared, std::tuple, std::shared_ptr, std::size_t;

static int CheckSizeAndCastToInt(const uint32_t size)
{
  if (size > static_cast<uint32_t>(std::numeric_limits<int>::max()))
  {
    throw std::overflow_error("Error: size too large: " + std::to_string(size));
  }

  return static_cast<int>(size);
}

/// Creates a CZI with four subblock of size 2x2 of pixeltype "Gray8" in a
/// mosaic arrangement. The arrangement is as follows:
/// +--+--+
/// |0 |1 |
/// |  |  |
/// +--+--+
/// |2 |3 |
/// |  |  |
/// +--+--+
/// Subblock 0 contains the value 0x1, subblock 1 contains the value 0x2,
/// subblock 2 contains the value 0x3 and subblock 3 contains the value 0x4.
/// \returns    A blob containing a CZI document.
static tuple<shared_ptr<void>, size_t> CreateCziWithFourSubblockInMosaicArrangement()
{
  auto writer = libCZI::CreateCZIWriter();
  auto outStream = make_shared<CMemOutputStream>(0);

  auto spWriterInfo = make_shared<libCZI::CCziWriterInfo>(GUID{0x1234567, 0x89ab, 0xcdef, {1, 2, 3, 4, 5, 6, 7, 8}},  // NOLINT
                                                          libCZI::CDimBounds{{libCZI::DimensionIndex::C, 0, 1}},      // set a bounds for C
                                                          0, 3);  // set a bounds M : 0<=m<=0
  writer->Create(outStream, spWriterInfo);

  auto bitmap = CreateGray8BitmapAndFill(2, 2, 0x1);
  libCZI::AddSubBlockInfoStridedBitmap addSbBlkInfo;
  addSbBlkInfo.Clear();
  addSbBlkInfo.coordinate.Set(libCZI::DimensionIndex::C, 0);
  addSbBlkInfo.mIndexValid = true;
  addSbBlkInfo.mIndex = 0;
  addSbBlkInfo.x = 0;
  addSbBlkInfo.y = 0;
  addSbBlkInfo.logicalWidth = CheckSizeAndCastToInt(bitmap->GetWidth());
  addSbBlkInfo.logicalHeight = CheckSizeAndCastToInt(bitmap->GetHeight());
  addSbBlkInfo.physicalWidth = CheckSizeAndCastToInt(bitmap->GetWidth());
  addSbBlkInfo.physicalHeight = CheckSizeAndCastToInt(bitmap->GetHeight());
  addSbBlkInfo.PixelType = bitmap->GetPixelType();
  {
    const libCZI::ScopedBitmapLockerSP lock_info_bitmap{bitmap};
    addSbBlkInfo.ptrBitmap = lock_info_bitmap.ptrDataRoi;
    addSbBlkInfo.strideBitmap = lock_info_bitmap.stride;
    writer->SyncAddSubBlock(addSbBlkInfo);
  }

  const uint16_t width = 2;
  const uint16_t height = 2;
  bitmap = CreateGray8BitmapAndFill(width, height, 0x2);
  addSbBlkInfo.Clear();
  addSbBlkInfo.coordinate.Set(libCZI::DimensionIndex::C, 0);
  addSbBlkInfo.mIndexValid = true;
  addSbBlkInfo.mIndex = 1;
  addSbBlkInfo.x = 2;
  addSbBlkInfo.y = 0;
  addSbBlkInfo.logicalWidth = width;
  addSbBlkInfo.logicalHeight = height;
  addSbBlkInfo.physicalWidth = width;
  addSbBlkInfo.physicalHeight = height;
  addSbBlkInfo.PixelType = bitmap->GetPixelType();
  {
    const libCZI::ScopedBitmapLockerSP lock_info_bitmap{bitmap};
    addSbBlkInfo.ptrBitmap = lock_info_bitmap.ptrDataRoi;
    addSbBlkInfo.strideBitmap = lock_info_bitmap.stride;
    writer->SyncAddSubBlock(addSbBlkInfo);
  }

  bitmap = CreateGray8BitmapAndFill(width, height, 0x3);
  addSbBlkInfo.Clear();
  addSbBlkInfo.coordinate.Set(libCZI::DimensionIndex::C, 0);
  addSbBlkInfo.mIndexValid = true;
  addSbBlkInfo.mIndex = 2;
  addSbBlkInfo.x = 0;
  addSbBlkInfo.y = 2;
  addSbBlkInfo.logicalWidth = width;
  addSbBlkInfo.logicalHeight = height;
  addSbBlkInfo.physicalWidth = width;
  addSbBlkInfo.physicalHeight = height;
  addSbBlkInfo.PixelType = bitmap->GetPixelType();
  {
    const libCZI::ScopedBitmapLockerSP lock_info_bitmap{bitmap};
    addSbBlkInfo.ptrBitmap = lock_info_bitmap.ptrDataRoi;
    addSbBlkInfo.strideBitmap = lock_info_bitmap.stride;
    writer->SyncAddSubBlock(addSbBlkInfo);
  }

  bitmap = CreateGray8BitmapAndFill(width, height, 0x4);
  addSbBlkInfo.Clear();
  addSbBlkInfo.coordinate.Set(libCZI::DimensionIndex::C, 0);
  addSbBlkInfo.mIndexValid = true;
  addSbBlkInfo.mIndex = 3;
  addSbBlkInfo.x = 2;
  addSbBlkInfo.y = 2;
  addSbBlkInfo.logicalWidth = width;
  addSbBlkInfo.logicalHeight = height;
  addSbBlkInfo.physicalWidth = width;
  addSbBlkInfo.physicalHeight = height;
  addSbBlkInfo.PixelType = bitmap->GetPixelType();
  {
    const libCZI::ScopedBitmapLockerSP lock_info_bitmap{bitmap};
    addSbBlkInfo.ptrBitmap = lock_info_bitmap.ptrDataRoi;
    addSbBlkInfo.strideBitmap = lock_info_bitmap.stride;
    writer->SyncAddSubBlock(addSbBlkInfo);
  }

  const libCZI::PrepareMetadataInfo prepare_metadata_info;
  auto metaDataBuilder = writer->GetPreparedMetadata(prepare_metadata_info);

  // NOLINTNEXTLINE: uninitialized struct is OK b/o Clear()
  libCZI::WriteMetadataInfo write_metadata_info;
  write_metadata_info.Clear();
  const auto& strMetadata = metaDataBuilder->GetXml();
  write_metadata_info.szMetadata = strMetadata.c_str();
  write_metadata_info.szMetadataSize = strMetadata.size() + 1;
  write_metadata_info.ptrAttachment = nullptr;
  write_metadata_info.attachmentSize = 0;
  writer->SyncWriteMetadata(write_metadata_info);
  writer->Close();
  writer.reset();

  size_t czi_document_size = 0;
  const shared_ptr<void> czi_document_data = outStream->GetCopy(&czi_document_size);
  return make_tuple(czi_document_data, czi_document_size);
}

TEST_CASE("copyczi.1: run compression on simple synthetic document", "[copyczi]")
{
  // arrange

  // call a helper function which creates a simple synthetic document in memory
  // (we get a pointer to the memory and the size of the memory)
  auto czi_document_as_blob = CreateCziWithFourSubblockInMosaicArrangement();

  // create a stream object from this memory
  const auto memory_stream = make_shared<CMemInputOutputStream>(std::get<0>(czi_document_as_blob).get(), std::get<1>(czi_document_as_blob));

  // now, construct a CZI-reader object on this stream
  const auto reader = libCZI::CreateCZIReader();
  reader->Open(memory_stream);

  // create a CZI-writer object on a new memory stream
  auto writer = libCZI::CreateCZIWriter();
  const auto memory_backed_stream_destination_document = make_shared<CMemInputOutputStream>(0);
  const auto writer_info = make_shared<libCZI::CCziWriterInfo>(GUID{0x0, 0x0, 0x0, {0, 0, 0, 0, 0, 0, 0, 0}});
  writer->Create(memory_backed_stream_destination_document, writer_info);

  // act
  {
    // ok, we now pass the reader and the writer to the copyCziAndCompress
    // object and run it
    CopyCziAndCompress copyCziAndCompress(reader, writer, nullptr, CompressionStrategy::kOnlyUncompressed,
                                          libCZI::Utils::ParseCompressionOptions("zstd1:"));
    copyCziAndCompress.Run();
  }

  writer->Close();  // this is important, otherwise the output stream will not
                    // be complete
  writer.reset();   // not really necessary, but it's good practice to reset the
                    // writer object after we're done with it

  // assert

  // and now, since the stream we used for the writer is a
  // read-write-memory-stream, we can pass the same
  //  stream object to a new CZI-reader object, which we can then use to check
  //  if the result is as we expect
  const auto reader_compressed_document = libCZI::CreateCZIReader();
  reader_compressed_document->Open(memory_backed_stream_destination_document);

  const auto statistics = reader_compressed_document->GetStatistics();
  REQUIRE(statistics.subBlockCount == 4);

  // now, let's check whether those subblocks are all zstd1 compressed
  for (int i = 0; i < 4; ++i)
  {
    libCZI::SubBlockInfo subblock_info;
    REQUIRE(reader_compressed_document->TryGetSubBlockInfo(i, &subblock_info) == true);
    REQUIRE(subblock_info.GetCompressionMode() == libCZI::CompressionMode::Zstd1);
  }
}

TEST_CASE("copyczi.2: run compression on simple synthetic document changes compression method in metadata.", "[copyczi]")
{
  // arrange

  // call a helper function which creates a simple synthetic document in memory
  // (we get a pointer to the memory and the size of the memory)
  auto czi_document_as_blob = CreateCziWithFourSubblockInMosaicArrangement();

  // create a stream object from this memory
  const auto memory_stream = make_shared<CMemInputOutputStream>(std::get<0>(czi_document_as_blob).get(), std::get<1>(czi_document_as_blob));

  // now, construct a CZI-reader object on this stream
  const auto reader = libCZI::CreateCZIReader();
  reader->Open(memory_stream);

  // create a CZI-writer object on a new memory stream
  auto writer = libCZI::CreateCZIWriter();
  const auto memory_backed_stream_destination_document = make_shared<CMemInputOutputStream>(0);
  const auto writer_info = make_shared<libCZI::CCziWriterInfo>(GUID{0x0, 0x0, 0x0, {0, 0, 0, 0, 0, 0, 0, 0}});
  writer->Create(memory_backed_stream_destination_document, writer_info);

  // act
  {
    // ok, we now pass the reader and the writer to the copyCziAndCompress
    // object and run it
    CopyCziAndCompress copyCziAndCompress(reader, writer, nullptr, CompressionStrategy::kOnlyUncompressed,
                                          libCZI::Utils::ParseCompressionOptions("zstd1:"));
    copyCziAndCompress.Run();
  }

  writer->Close();  // this is important, otherwise the output stream will not
                    // be complete
  writer.reset();   // not really necessary, but it's good practice to reset the
                    // writer object after we're done with it

  // assert

  // and now, since the stream we used for the writer is a
  // read-write-memory-stream, we can pass the same
  //  stream object to a new CZI-reader object, which we can then use to check
  //  if the result is as we expect
  const auto reader_compressed_document = libCZI::CreateCZIReader();
  reader_compressed_document->Open(memory_backed_stream_destination_document);

  auto metadata_segment = reader_compressed_document->ReadMetadataSegment();
  auto metadata = metadata_segment->CreateMetaFromMetadataSegment();

  auto compression_method = metadata->GetChildNodeReadonly("ImageDocument/Metadata/Information/Image/OriginalCompressionMethod");
  std::wstring compression_method_string;
  bool success = compression_method->TryGetValue(&compression_method_string);
  REQUIRE(success == true);
  REQUIRE(compression_method_string == std::wstring(L"Zstd1"));

  std::wstring compression_level;
  auto encoding_quality = metadata->GetChildNodeReadonly("ImageDocument/Metadata/Information/Image/OriginalEncodingQuality");
  std::wstring encoding_quality_string;
  success = encoding_quality->TryGetValue(&encoding_quality_string);
  REQUIRE(success == true);
  REQUIRE(encoding_quality_string == std::wstring(L"100"));
}


TEST_CASE("copyczi.3: run decompression on simple synthetically compressed document changes compression method in metadata.", "[copyczi]")
{
  // arrange

  // call a helper function which creates a simple synthetic document in memory
  // (we get a pointer to the memory and the size of the memory)
  auto czi_document_as_blob = CreateCziWithFourSubblockInMosaicArrangement();

  // create a stream object from this memory
  const auto memory_stream = make_shared<CMemInputOutputStream>(std::get<0>(czi_document_as_blob).get(), std::get<1>(czi_document_as_blob));

  // now, construct a CZI-reader object on this stream
  const auto reader = libCZI::CreateCZIReader();
  reader->Open(memory_stream);

  // create a CZI-writer object on a new memory stream
  auto writer = libCZI::CreateCZIWriter();
  const auto memory_backed_stream_destination_document = make_shared<CMemInputOutputStream>(0);
  const auto writer_info = make_shared<libCZI::CCziWriterInfo>(GUID{0x0, 0x0, 0x0, {0, 0, 0, 0, 0, 0, 0, 0}});
  writer->Create(memory_backed_stream_destination_document, writer_info);

  // act
  {
    // ok, we now pass the reader and the writer to the copyCziAndCompress
    // object and run it
    CopyCziAndCompress copyCziAndCompress(reader, writer, nullptr, CompressionStrategy::kOnlyUncompressed,
                                          libCZI::Utils::ParseCompressionOptions("zstd1:"));
    copyCziAndCompress.Run();
  }

  writer->Close();  // this is important, otherwise the output stream will not
                    // be complete
  writer.reset();   // not really necessary, but it's good practice to reset the
                    // writer object after we're done with it

  // assert

  // and now, since the stream we used for the writer is a
  // read-write-memory-stream, we can pass the same
  //  stream object to a new CZI-reader object, which we can then use to check
  //  if the result is as we expect
  const auto reader_compressed_document = libCZI::CreateCZIReader();
  reader_compressed_document->Open(memory_backed_stream_destination_document);

  // create a CZI-writer object on a new memory stream
  auto writer_2 = libCZI::CreateCZIWriter();
  const auto memory_backed_stream_destination_document_2 = make_shared<CMemInputOutputStream>(0);
  const auto writer_info_2 = make_shared<libCZI::CCziWriterInfo>(GUID{0x0, 0x0, 0x0, {0, 0, 0, 0, 0, 0, 0, 0}});
  writer_2->Create(memory_backed_stream_destination_document_2, writer_info_2);

  // act
  {
    // ok, we now pass the reader and the writer to the copyCziAndCompress
    // object and run it
    CopyCziAndDecompress copyCziAndDecompress(reader_compressed_document, writer_2, nullptr);
    copyCziAndDecompress.Run();
  }

  writer_2->Close();  // this is important, otherwise the output stream will not
                      // be complete
  writer_2.reset();   // not really necessary, but it's good practice to reset the
                      // writer object after we're done with it

  const auto reader_decompressed_document = libCZI::CreateCZIReader();
  reader_decompressed_document->Open(memory_backed_stream_destination_document_2);

  auto metadata_segment = reader_decompressed_document->ReadMetadataSegment();
  auto metadata = metadata_segment->CreateMetaFromMetadataSegment();

  auto compression_method = metadata->GetChildNodeReadonly("ImageDocument/Metadata/Information/Image/OriginalCompressionMethod");
  auto xml = metadata->GetXml();
  std::wstring compression_method_string;
  bool success = compression_method->TryGetValue(&compression_method_string);
  REQUIRE(success == true);
  REQUIRE(compression_method_string == std::wstring(L"Uncompressed"));

  std::wstring compression_level;
  auto encoding_quality = metadata->GetChildNodeReadonly("ImageDocument/Metadata/Information/Image/OriginalEncodingQuality");
  std::wstring encoding_quality_string;
  success = encoding_quality->TryGetValue(&encoding_quality_string);
  REQUIRE(success == true);
  REQUIRE(encoding_quality_string == std::wstring(L"100"));
}
