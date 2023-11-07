// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Customizations;

using Xunit.Sdk;

public class ValidInlineAutoData : CompositeDataAttribute
{
    public ValidInlineAutoData(params object[] values)
        : base(new DataAttribute[]
        {
            new InlineDataAttribute(values),
            new CorrectValueObjectAutoDataAttribute(),
        })
    {
    }
}