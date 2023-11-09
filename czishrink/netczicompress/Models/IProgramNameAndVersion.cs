// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

/// <summary>
/// An object that provides program name and version info.
/// </summary>
public interface IProgramNameAndVersion
{
    string Name { get; }

    string Version { get; }
}