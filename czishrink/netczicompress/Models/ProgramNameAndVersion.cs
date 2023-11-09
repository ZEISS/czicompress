// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.Linq;
using System.Reflection;

using netczicompress.ViewModels;

/// <summary>
/// Program name and version info.
/// </summary>
public record class ProgramNameAndVersion : IProgramNameAndVersion
{
    public ProgramNameAndVersion()
    {
        var assembly = typeof(AboutViewModel).Assembly;
        this.Version = assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().Select(att => att.InformationalVersion).FirstOrDefault() ??
            assembly.GetName().Version?.ToString() ??
            throw new ApplicationException("Failed to determine assembly version");
        this.Name = "CZI Shrink";
    }

    public string Name { get; }

    public string Version { get; }

    public override string ToString()
    {
        return $"{this.Name} {this.Version}";
    }
}
