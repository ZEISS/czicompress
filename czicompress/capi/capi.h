// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: MIT

#pragma once

#include <stdint.h>

#include <cstdint>

#include "capi_export.h"
#include "include/command.h"
#include "include/compressionstrategy.h"
#include <stddef.h>

// input_path is only guaranteed to exist during the duration of this call and should be copied if retained
typedef bool (*ProgressReport)(int32_t progress_percent); // NOLINT(readability/casting)

/**
 * Processes a single file with the specified file processor.
 *
 * @param  file_processor        A file processor pointer obtained with CreateFileProcessor().
 * @param  input_path            The UTF8-encoded, null-terminated path of the file to process.
 * @param  output_path           The UTF8-encoded, null-terminated path of the file to create and write to.
 * @param  error_message         A character buffer to which a UTF8-encoded error message can be written in case of failure.
 * @param  error_message_length  Initially, the size of the error_message buffer. If the method fails (returns non-zero),
 *                               this parameter will be set to the size of the (cropped if necessary) error_message string.
 * @param progress_report        A function pointer called to report progress and check cancellation.
 *
 * @returns    Zero (0) in case of success, a non-zero value in case of failure.
 */
extern "C" CAPI_EXPORT int ProcessFile(void* file_processor, const char* const input_path, const char* const output_path,
                                       char* error_message, size_t* error_message_length, ProgressReport progress_report);
/**
 * Creates a new file processor for use in ProcessFile().
 *
 *  @param command  The #Command to use
 *  @param strategy The #CompressionStrategy to use if the command is a compression command (ignored for decompression)
 *  @param compression_level The level of compression to use if applicable (ignored for decompression)
 *
 * @returns    A new file processor for the specified command and compression strategy.
 */
extern "C" CAPI_EXPORT void* CreateFileProcessor(Command command, CompressionStrategy strategy, int compression_level);

/**
 * Destroys a file processor after use.
 *
 * @param file_processor   A file processor pointer obtained with CreateFileProcessor().
 */
extern "C" CAPI_EXPORT void DestroyFileProcessor(void* file_processor);

/**
 * Gets the version number of czicompress.
 *
 * @param major   Set to the major version.
 * @param minor   Set to the minor version.
 * @param path    Set to the patch version.
 */
extern "C" CAPI_EXPORT void GetLibVersion(int32_t* major, int32_t* minor, int32_t* patch);

/**
 * Gets a string containing the version number of czicompress (a null-terminated string in UTF8-encoding).
 * The caller must pass in a pointer to a buffer and a pointer to an uint64, where the latter
 * must contain the length of the buffer in bytes. If the length of the buffer is sufficient,
 * then the string is copied into the buffer and the function returns false.
 * If the size of the buffer is insufficient, then the required size is written into '*size'
 * and the return value is false. In this case, the caller may increase the buffer size (to at
 * least the number given with the first call) and call again.
 *
 *  @param buffer   Pointer to a buffer (which size is stated with size).
 *  @param size     Pointer to an uint64 which on input contains the size of the buffer, and on output (with return value false) 
 *                  the required size of the buffer. 
 *
 * @returns    True if the buffer size was sufficient (and in this case the text is copied to the buffer); false otherwise (and in 
 *             this case the required size is written to *size).
 */
extern "C" CAPI_EXPORT bool GetLibVersionString(char* buffer, uint64_t* size);
