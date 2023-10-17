// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#include "libczi_utils.h"

#include <libCZI.h>

#include <algorithm>

CMemOutputStream::CMemOutputStream(size_t initial_size) : ptr_(nullptr), allocated_size_(initial_size), used_size_(0)
{
  if (initial_size > 0)
  {
    this->ptr_ = static_cast<char*>(malloc(initial_size));  // NOLINT
  }
}

/*virtual*/ CMemOutputStream::~CMemOutputStream()
{
  free(this->ptr_);  // NOLINT
}

/*virtual*/ void CMemOutputStream::Write(std::uint64_t offset, const void* data, std::uint64_t size, std::uint64_t* ptr_bytes_written)
{
  this->EnsureSize(offset + size);
  auto* dst = this->ptr_ + offset;  // NOLINT: pointer arithmetic
  memcpy(dst, data, size);
  this->used_size_ = (std::max)(static_cast<size_t>(offset + size), this->used_size_);
  if (ptr_bytes_written != nullptr)
  {
    *ptr_bytes_written = size;
  }
}

void CMemOutputStream::EnsureSize(std::uint64_t new_size)
{
  if (new_size > this->allocated_size_)
  {
    new_size = (std::max)(new_size, static_cast<std::uint64_t>(this->allocated_size_ + this->allocated_size_ / 4));
    this->ptr_ = static_cast<char*>(realloc(this->ptr_, new_size));  // NOLINT
    this->allocated_size_ = new_size;
  }
}

// ======================================================================================================

CMemInputOutputStream::CMemInputOutputStream(size_t initial_size) : ptr_(nullptr), allocated_size_(initial_size), used_size_(0)
{
  if (initial_size > 0)
  {
    this->ptr_ = static_cast<char*>(malloc(initial_size));  // NOLINT
  }
}

CMemInputOutputStream::CMemInputOutputStream(const void* data, size_t size) : CMemInputOutputStream(size)
{
  this->Write(0, data, size, nullptr);
}

/*virtual*/ CMemInputOutputStream::~CMemInputOutputStream()
{
  free(this->ptr_);  // NOLINT
}

/*virtual*/ void CMemInputOutputStream::Write(std::uint64_t offset, const void* data, std::uint64_t size, std::uint64_t* ptr_bytes_written)
{
  this->EnsureSize(offset + size);
  auto* src = this->ptr_ + offset;  // NOLINT: pointer arithmetic
  memcpy(src, data, size);
  this->used_size_ = (std::max)(static_cast<size_t>(offset + size), this->used_size_);
  if (ptr_bytes_written != nullptr)
  {
    *ptr_bytes_written = size;
  }
}

/*virtual*/ void CMemInputOutputStream::Read(std::uint64_t offset, void* data, std::uint64_t size, std::uint64_t* ptr_bytes_read)
{
  if (offset < this->used_size_)
  {
    const size_t size_to_copy = (std::min)(static_cast<size_t>(size), static_cast<size_t>(this->used_size_ - offset));
    auto* src = this->ptr_ + offset;  // NOLINT: pointer arithmetic
    memcpy(data, src, size_to_copy);
    if (ptr_bytes_read != nullptr)
    {
      *ptr_bytes_read = size_to_copy;
    }
  }
  else
  {
    if (ptr_bytes_read != nullptr)
    {
      *ptr_bytes_read = 0;
    }
  }
}

void CMemInputOutputStream::EnsureSize(std::uint64_t newSize)
{
  if (newSize > this->allocated_size_)
  {
    this->ptr_ = static_cast<char*>(realloc(this->ptr_, newSize));  // NOLINT
    this->allocated_size_ = newSize;
  }
}

// ======================================================================================================

class CMemBitmapWrapper : public libCZI::IBitmapData
{
private:
  const int Bgr24_BPP = 3;
  const int Bgr48_BPP = 6;
  const int Gray16_BPP = 2;
  const int Gray32F_BPP = 4;
  void* ptrData_;
  libCZI::PixelType pixeltype_;
  std::uint32_t width_;
  std::uint32_t height_;
  std::uint32_t stride_;

public:
  CMemBitmapWrapper(libCZI::PixelType pixeltype, std::uint32_t width, std::uint32_t height)
      : pixeltype_(pixeltype), width_(width), height_(height)
  {
    int bytes_per_pel = 0;
    switch (pixeltype)
    {
      case libCZI::PixelType::Bgr24:
        bytes_per_pel = Bgr24_BPP;
        break;
      case libCZI::PixelType::Bgr48:
        bytes_per_pel = Bgr48_BPP;
        break;
      case libCZI::PixelType::Gray8:
        bytes_per_pel = 1;
        break;
      case libCZI::PixelType::Gray16:
        bytes_per_pel = Gray16_BPP;
        break;
      case libCZI::PixelType::Gray32Float:
        bytes_per_pel = Gray32F_BPP;
        break;
      default:
        throw std::runtime_error("unsupported pixeltype");
    }

    if (pixeltype == libCZI::PixelType::Bgr24)
    {
      this->stride_ = ((width * bytes_per_pel + 3) / 4) * 4;
    }
    else
    {
      this->stride_ = width * bytes_per_pel;
    }

    const size_t size = static_cast<size_t>(this->stride_) * height;
    this->ptrData_ = malloc(size);  // NOLINT
  }

  ~CMemBitmapWrapper() override
  {
    free(this->ptrData_);  // NOLINT
  };

  libCZI::PixelType GetPixelType() const override { return this->pixeltype_; }

  libCZI::IntSize GetSize() const override { return libCZI::IntSize{this->width_, this->height_}; }

  libCZI::BitmapLockInfo Lock() override
  {
    libCZI::BitmapLockInfo bitmapLockInfo{
        this->ptrData_,
        this->ptrData_,
        this->stride_,
        static_cast<uint64_t>(this->stride_) * this->height_,
    };

    return bitmapLockInfo;
  }

  void Unlock() override {}
};

std::shared_ptr<libCZI::IBitmapData> CreateGray8BitmapAndFill(std::uint32_t width, std::uint32_t height, uint8_t value)
{
  auto bitmap = std::make_shared<CMemBitmapWrapper>(libCZI::PixelType::Gray8, width, height);
  const libCZI::ScopedBitmapLockerSP bitmap_locked{bitmap};
  auto* data = static_cast<uint8_t*>(bitmap_locked.ptrDataRoi);
  auto stride = static_cast<size_t>(bitmap_locked.stride);
  for (uint32_t row = 0; row < height; ++row)
  {
    uint8_t* row_start_address = data + (stride * row);  // NOLINT: pointer arithmetic
    memset(row_start_address, value, width);
  }

  return bitmap;
}
