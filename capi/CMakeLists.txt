# SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
#
# SPDX-License-Identifier: MIT

message (STATUS "[${PROJECT_NAME}] Processing ${CMAKE_CURRENT_LIST_FILE}")

set (TARGET_NAME lib${PROJECT_NAME}c)

add_library (${TARGET_NAME} SHARED
    capi.h
    capi.cpp
)

# with GCC (on Linux) we want to link statically to libstd++ - this should increase the likelihood that the
# binary works on various distros (c.f. https://stackoverflow.com/questions/13636513/linking-libstdc-statically-any-gotchas)
if(CMAKE_CXX_COMPILER_ID STREQUAL "GNU")
  target_link_options(${TARGET_NAME} PRIVATE -static-libstdc++)
endif()

if(WIN32)
  set(CZICOMPRESS_RCVersion ${czicompress_VERSION_MAJOR},${czicompress_VERSION_MINOR},${czicompress_VERSION_PATCH},${czicompress_VERSION_TWEAK})
  set(CZICOMPRESS_RCVersion_STR ${czicompress_VERSION_MAJOR_STR},${czicompress_VERSION_MINOR_STR},${czicompress_VERSION_PATCH_STR},${czicompress_VERSION_TWEAK_STR})

  configure_file (
    CZICompress_Resource.rc.in CZICompress_Resource.rc @ONLY
  )

  add_library(${TARGET_NAME}_Version OBJECT "${CMAKE_CURRENT_BINARY_DIR}/CZICompress_Resource.rc")
  set_property(TARGET ${TARGET_NAME}_Version PROPERTY INCLUDE_DIRECTORIES "")
  target_link_libraries(${TARGET_NAME} PRIVATE ${TARGET_NAME}_Version)

  target_compile_options(${TARGET_NAME} PUBLIC "$<$<C_COMPILER_ID:MSVC>:/utf-8>")
  target_compile_options(${TARGET_NAME} PUBLIC "$<$<CXX_COMPILER_ID:MSVC>:/utf-8>")
endif()

set_target_properties(${TARGET_NAME} PROPERTIES PREFIX "")

include(GenerateExportHeader)
GENERATE_EXPORT_HEADER(${TARGET_NAME}
    BASE_NAME CAPI)

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
