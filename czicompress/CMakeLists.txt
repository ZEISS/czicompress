# SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
#
# SPDX-License-Identifier: MIT

cmake_minimum_required(VERSION 3.2 FATAL_ERROR)

cmake_policy(SET CMP0091 NEW) # enable new "MSVC runtime library selection" (https://cmake.org/cmake/help/latest/variable/CMAKE_MSVC_RUNTIME_LIBRARY.html)

# Note that the CMake-variables <Projectname>_VERSION_MAJOR, <Projectname>_VERSION_MINOR, ... are defined by this statement,
#  which are used in the build (so that - if changing the name here, change also the usage of those variables).
project ("czicompress"
         VERSION 0.5.3)

set(czicompress_VERSION_PATCH_FLAGS "")
set(czicompress_VERSION_TWEAK_FLAGS "")

if(WIN32)
  # use "static C-runtime" -> https://stackoverflow.com/questions/14172856/compile-with-mt-instead-of-md-using-cmake
  # Note: this requires CMAKE version 3.15+
  set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>")
endif(WIN32)

include(cmake/SemVer.cmake)

message (STATUS "[${PROJECT_NAME}] Processing ${CMAKE_CURRENT_LIST_FILE}")

# make libCZI available
include(FetchContent)
FetchContent_Declare(
  libczi
  GIT_REPOSITORY https://github.com/ZEISS/libczi.git
  GIT_TAG        05bc1ebdca520ba2c5bfc3b9fcde76c548e98545
)

if(NOT libCZI_POPULATED)
  FetchContent_Populate(libCZI)

  # we don't need to build unittests and such, and no dynamic library
  set(LIBCZI_BUILD_CZICMD OFF CACHE BOOL "" FORCE)
  set(LIBCZI_BUILD_DYNLIB OFF CACHE BOOL "" FORCE)
  set(LIBCZI_BUILD_UNITTESTS OFF CACHE BOOL "" FORCE)
  set(LIBCZI_DO_NOT_SET_MSVC_RUNTIME_LIBRARY  ON CACHE BOOL "" FORCE)

  add_subdirectory(${libczi_SOURCE_DIR} ${libczi_BINARY_DIR})
endif()

FetchContent_GetProperties(libCZI)
set(LIBCZI_INCLUDE_DIR "${libczi_SOURCE_DIR}/Src/libCZI")

set (TARGET_NAME ${PROJECT_NAME})

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

add_subdirectory(lib)
add_subdirectory(capi)
add_subdirectory(app)

# With this option one can choose between "downloading a private copy of catch2 during build" or use
#  an existing catch2 (on the system). If CMake is unable to find a catch2-package (and this option is
#  "ON"), the build will fail.
option(CZICOMPRESS_BUILD_PREFER_EXTERNALPACKAGE_CATCH2 "Prefer an catch2-package present on the system" OFF)

# With this option one can choose between "downloading a private copy of CLI11 during build" or use
#  an existing CLI11 (on the system). If CMake is unable to find a CLI11-package (and this option is
#  "ON"), the build will fail.
option(CZICOMPRESS_BUILD_PREFER_EXTERNALPACKAGE_CLI11 "Prefer an CLI11-package present on the system" OFF)

enable_testing()
add_subdirectory(tests)
