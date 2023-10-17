# SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
#
# SPDX-License-Identifier: MIT

message (STATUS "[${PROJECT_NAME}] Processing ${CMAKE_CURRENT_LIST_FILE}")
set (TARGET_NAME lib${PROJECT_NAME})

if(CZICOMPRESS_BUILD_PREFER_EXTERNALPACKAGE_CLI11)
    find_package(CLI11 CONFIG REQUIRED)
else()
    include(FetchContent)
    FetchContent_Declare(
      cli11
      GIT_REPOSITORY https://github.com/CLIUtils/CLI11
      GIT_TAG        v2.3.2
      )

  FetchContent_MakeAvailable(cli11)
endif()

# prepare the CZICompress_Config.h file - which is then to be found in the binary directory ${CMAKE_CURRENT_BINARY_DIR}
IF(WIN32)
    set(CZICompress_UNIX_ENVIRONMENT 0)
    set(CZICompress_WIN32_ENVIRONMENT 1)
    set(CZICompress_PLATFORM_TAG "windows")
ENDIF()
IF(UNIX)
    set(CZICompress_UNIX_ENVIRONMENT 1)
    set(CZICompress_WIN32_ENVIRONMENT 0)
    set(CZICompress_PLATFORM_TAG "posix") # Currently we assume that code pulled in via PLATFORM_TAG will work on either UNIX or APPLE in this scenario.
ENDIF()

add_library(${TARGET_NAME}
    "inc_libCZI.h"
    "include/utils/utf8/utf8converter.h"
    "include/commandlineoptions.h"
    "src/operation.cpp" 
    "src/operation.h" 
    "src/copyczi.h" 
    "src/copyczi.cpp" 
    "src/commandlineoptions.cpp" 
    "include/IConsoleio.h" 
    "src/consoleio.h"
    "src/actionwithsubblockstatistics.h"
    "src/consoleio.cpp"
    "include/command.h" 
    "include/progressinfo.h" 
    "src/progressinfo.cpp" )

configure_file (
  "${CMAKE_CURRENT_SOURCE_DIR}/CZICompress_Config.h.in"
  "${CMAKE_CURRENT_BINARY_DIR}/CZICompress_Config.h")

# Windows specific utility functions for handling global error printing
if (CZICompress_WIN32_ENVIRONMENT)
    target_sources(${TARGET_NAME} PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include/utils/errorhandling/winerrorformatting.h
    ${CMAKE_CURRENT_SOURCE_DIR}/src/utils/errorhandling/winerrorformatting.cpp)
endif()

target_sources(${TARGET_NAME} PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/src/utils/utf8/platforms/${CZICompress_PLATFORM_TAG}/utf8converter.cpp)
# this is necessary for statically linking the libCZI library
target_compile_definitions(${TARGET_NAME} PUBLIC _LIBCZISTATICLIB)

target_link_libraries(${TARGET_NAME} PUBLIC libCZIStatic PRIVATE CLI11::CLI11)

# Make sure the compiler can find include files for our library
# when other libraries or executables link to ${TARGET_NAME}
target_include_directories (${TARGET_NAME} 
                              SYSTEM PUBLIC ${LIBCZI_INCLUDE_DIR} ${CMAKE_CURRENT_SOURCE_DIR}
                              PRIVATE ${CMAKE_CURRENT_BINARY_DIR})
