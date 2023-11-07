// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Customizations;

using AutoFixture.Kernel;

/// <summary>
/// CompressionLevel specimen builder that will create only valid CompressionLevels.
/// </summary>
/// <remarks>
/// This builder depends on Min/Max values of CompressionLevel being public to avoid magic values.
/// </remarks>
public class CompressionLevelSpecimenBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (request is Type type && type == typeof(CompressionLevel))
        {
            var value = new Random().Next(CompressionLevel.Minimum, CompressionLevel.Maximum + 1);
            return new CompressionLevel { Value = value };
        }

        return new NoSpecimen();
    }
}