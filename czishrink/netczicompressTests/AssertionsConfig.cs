// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests;

using System.Runtime.CompilerServices;
using System.Runtime.Versioning;

internal static class AssertionsConfig
{
    [ModuleInitializer]
    public static void SetDefaults()
    {
        AssertionConfiguration.Current.Equivalency.Modify(
            options => options
                .WithStrictOrdering()
                .Using(
                    new ExcludePropertiesByAttributeSelectionRule(
                        typeof(OSPlatformAttribute))));
    }
}
