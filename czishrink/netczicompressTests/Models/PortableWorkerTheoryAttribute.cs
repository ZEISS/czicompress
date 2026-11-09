// SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

/// <summary>Runs worker-process cases with an explicitly supplied local Python runtime.</summary>
public sealed class PortableWorkerTheoryAttribute : TheoryAttribute
{
    public PortableWorkerTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(Environment.GetEnvironmentVariable("CZISHRINK_TEST_PYTHON")))
        {
            this.Skip = "Requires Windows and an explicit local CZISHRINK_TEST_PYTHON runtime.";
        }
    }
}
