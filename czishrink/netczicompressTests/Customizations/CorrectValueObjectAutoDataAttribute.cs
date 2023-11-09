// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Customizations;

public class CorrectValueObjectAutoDataAttribute : AutoDataAttribute
{
    public CorrectValueObjectAutoDataAttribute()
        : base(() =>
    {
        var fixture = new Fixture();
        fixture.Customizations.Add(new CompressionLevelSpecimenBuilder());
        fixture.Customizations.Add(new ThreadCountSpecimenBuilder());
        return fixture;
    })
    {
    }
}