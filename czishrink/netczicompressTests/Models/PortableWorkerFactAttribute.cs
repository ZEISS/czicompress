// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

/// <summary>Runs only when an explicitly supplied local portable Python is available.</summary>
public sealed class PortableWorkerFactAttribute : FactAttribute
{
    public PortableWorkerFactAttribute()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(Environment.GetEnvironmentVariable("CZISHRINK_TEST_PYTHON")))
        {
            this.Skip = "Requires Windows and an explicit local CZISHRINK_TEST_PYTHON runtime.";
        }
    }
}
