// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Customizations;

using AutoFixture.Kernel;

/// <summary>
/// <see cref="ThreadCount"/> specimen builder that will create only valid <see cref="ThreadCount"/>s.
/// </summary>
/// <remarks>
/// This builder depends on Min/Max values of <see cref="ThreadCount"/> being public to avoid magic values.
/// </remarks>
public class ThreadCountSpecimenBuilder : ISpecimenBuilder
{
    public object Create(object request, ISpecimenContext context)
    {
        if (request is Type type && type == typeof(ThreadCount))
        {
            var value = new Random().Next(ThreadCount.Minimum, ThreadCount.Maximum + 1);
            return new ThreadCount { Value = value };
        }

        return new NoSpecimen();
    }
}