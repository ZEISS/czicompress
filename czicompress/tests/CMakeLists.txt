# SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
#
# SPDX-License-Identifier: MIT

message (STATUS "[${PROJECT_NAME}] Processing ${CMAKE_CURRENT_LIST_FILE}")
set (TARGET_NAME ${PROJECT_NAME}_tests)

if(CZICOMPRESS_BUILD_PREFER_EXTERNALPACKAGE_CATCH2)
  # either we use Catch2 from the system's package manager
  find_package(Catch2 3 REQUIRED)
else()
  # ...or we clone the Catch2 repository from GitHub and build it as part of our project
  Include(FetchContent)
  FetchContent_Declare(
  Catch2
  GIT_REPOSITORY https://github.com/catchorg/Catch2.git
  GIT_TAG        v3.4.0 # or a later release
  )
  FetchContent_MakeAvailable(Catch2)
endif()

add_executable(${TARGET_NAME} 
  "libczi_utils.h"
  "libczi_utils.cpp"
  "test_commandlineparsing.cpp"
  "test_copyoperation.cpp"
  "test_utf8_utils.cpp"
)

target_link_libraries (${TARGET_NAME}
    PRIVATE Catch2::Catch2WithMain
    PRIVATE lib${PROJECT_NAME}
)

list(APPEND CMAKE_MODULE_PATH ${catch2_SOURCE_DIR}/extras)

include(CTest)
include(Catch)
catch_discover_tests(${TARGET_NAME})
