// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.Text.RegularExpressions;

/// <summary>
/// Tests for <see cref="ProgramNameAndVersion"/>.
/// </summary>
public class ProgramNameAndVersionTests
{
    [Fact]
    public void ToString_WhenCalled_ReturnsExpected()
    {
        // ACT
        var actual = new ProgramNameAndVersion().ToString();

        // ASSERT
        actual.Should().MatchRegex(@"^CZI Shrink 1\.1\.\d+(\+\d+)?$");
    }

    [Fact]
    public void Name_WhenCalled_ReturnsExpected()
    {
        // ACT
        var actual = new ProgramNameAndVersion().Name;

        // ASSERT
        actual.Should().Be("CZI Shrink");
    }

    [Fact]
    public void Version_WhenCalled_ReturnsExpected()
    {
        // ACT
        var actual = new ProgramNameAndVersion().Version;

        // ASSERT
        actual.Should().MatchRegex(@"^1\.1\.\d+(\+\d+)?$");
    }
}
