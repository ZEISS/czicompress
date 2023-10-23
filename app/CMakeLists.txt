# SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
#
# SPDX-License-Identifier: MIT

message (STATUS "[${PROJECT_NAME}] Processing ${CMAKE_CURRENT_LIST_FILE}")

set (TARGET_NAME ${PROJECT_NAME})

add_executable (${TARGET_NAME}
    CZIcompress.cpp
    commandlineargshelper.h
    commandlineargshelper.cpp
    czicompress-longpaths.manifest # the manifest is needed to allow long paths on windows, see https://docs.microsoft.com/en-us/windows/win32/fileio/maximum-file-path-limitation?tabs=cmd
                                   # CMake will merge the manifest with a linker generated one (and ignore it on non-windows platforms)
)

target_compile_options(${TARGET_NAME} PUBLIC "$<$<C_COMPILER_ID:MSVC>:/utf-8>")
target_compile_options(${TARGET_NAME} PUBLIC "$<$<CXX_COMPILER_ID:MSVC>:/utf-8>")

# prepare the CZICompress_Config.h file - which is then to be found in the binary directory ${CMAKE_CURRENT_BINARY_DIR}
IF(WIN32)
  set(CZICompress_UNIX_ENVIRONMENT 0)
  set(CZICompress_WIN32_ENVIRONMENT 1)
ENDIF()
IF(UNIX)
  set(CZICompress_UNIX_ENVIRONMENT 1)
  set(CZICompress_WIN32_ENVIRONMENT 0)
ENDIF()

configure_file (
  "${CMAKE_CURRENT_SOURCE_DIR}/CZICompress_Config.h.in"
  "${CMAKE_CURRENT_BINARY_DIR}/CZICompress_Config.h")

target_link_libraries (${TARGET_NAME}
    PRIVATE lib${PROJECT_NAME}
)

target_include_directories(${TARGET_NAME} PRIVATE ${CMAKE_CURRENT_BINARY_DIR})

# Copy license file from libczi_SOURCE_DIR
file(COPY "${libczi_SOURCE_DIR}/THIRD_PARTY_LICENSES.txt" DESTINATION "${CMAKE_CURRENT_BINARY_DIR}/")
