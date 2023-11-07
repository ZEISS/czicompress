// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests;

using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

internal static class FluentAssertionsConfig
{
    [ModuleInitializer]
    public static void SetDefaults()
    {
        AssertionOptions.AssertEquivalencyUsing(
            options => options
                .WithStrictOrdering()
                .Using(
                    new ExcludePropertiesByAttributeSelectionRule(
                        typeof(OSPlatformAttribute))));
    }
}
