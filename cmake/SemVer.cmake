# SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
#
# SPDX-License-Identifier: MIT

set(czicompress_VERSION_MAJOR_STR ${czicompress_VERSION_MAJOR})
set(czicompress_VERSION_MINOR_STR ${czicompress_VERSION_MINOR})
set(czicompress_VERSION_PATCH_STR ${czicompress_VERSION_PATCH})
set(czicompress_VERSION_TWEAK_STR ${czicompress_VERSION_TWEAK})

# If we do not define patch we ensure that it is set to 0
if("${czicompress_VERSION_PATCH}" STREQUAL "")
  set(czicompress_VERSION_PATCH 0)
  set(czicompress_VERSION_PATCH_STR 0)
endif()

# If we do not define tweak we ensure that it is set to 0
if("${czicompress_VERSION_TWEAK}" STREQUAL "")
  set(czicompress_VERSION_TWEAK 0)
  set(czicompress_VERSION_TWEAK_STR 0)
endif()

# Current assumption is that flags is a string rather than a list
set(czicompress_VERSION_PATCH_STR ${czicompress_VERSION_PATCH}${czicompress_VERSION_PATCH_FLAGS})
set(czicompress_VERSION_TWEAK_STR ${czicompress_VERSION_TWEAK}${czicompress_VERSION_TWEAK_FLAGS})