// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once

#include <libCZI.h>

#include <memory>

/// Implementation of libCZI::IOutputStream which is backed by a memory buffer.
class CMemOutputStream : public libCZI::IOutputStream
{
private:
  char* ptr_;
  size_t allocated_size_;
  size_t used_size_;

public:
  explicit CMemOutputStream(size_t initial_size);
  ~CMemOutputStream() override;

  const char* GetDataC() const { return this->ptr_; }

  size_t GetDataSize() const { return this->used_size_; }

  std::shared_ptr<void> GetCopy(size_t* ptr_size) const
  {
    auto buffer = std::shared_ptr<void>(malloc(this->used_size_), [](void* ptr) -> void { free(ptr); });
    memcpy(buffer.get(), this->ptr_, this->used_size_);
    if (ptr_size != nullptr)
    {
      *ptr_size = this->used_size_;
    }

    return buffer;
  }

public:  // interface IOutputStream
  void Write(std::uint64_t offset, const void* data, std::uint64_t size, std::uint64_t* ptr_bytes_written) override;

private:
  void EnsureSize(std::uint64_t new_size);
};

/// Implementation of libCZI::IInputOutputStream which is backed by a memory
/// buffer.
class CMemInputOutputStream : public libCZI::IInputOutputStream
{
private:
  char* ptr_;
  size_t allocated_size_;
  size_t used_size_;

public:
  CMemInputOutputStream(const void* data, size_t size);
  explicit CMemInputOutputStream(size_t initial_size);
  ~CMemInputOutputStream() override;

  const char* GetDataC() const { return this->ptr_; }

  size_t GetDataSize() const { return this->used_size_; }

  std::shared_ptr<void> GetCopy(size_t* pSize) const
  {
    auto buffer = std::shared_ptr<void>(malloc(this->used_size_), [](void* ptr) -> void { free(ptr); });
    memcpy(buffer.get(), this->ptr_, this->used_size_);
    if (pSize != nullptr)
    {
      *pSize = this->used_size_;
    }

    return buffer;
  }

public:  // interface IOutputStream
  void Write(std::uint64_t offset, const void* data, std::uint64_t size, std::uint64_t* ptr_bytes_written) override;

public:  // interface IStream
  void Read(std::uint64_t offset, void* data, std::uint64_t size, std::uint64_t* ptr_bytes_read) override;

private:
  void EnsureSize(std::uint64_t newSize);
};

std::shared_ptr<libCZI::IBitmapData> CreateGray8BitmapAndFill(std::uint32_t width, std::uint32_t height, uint8_t value);
