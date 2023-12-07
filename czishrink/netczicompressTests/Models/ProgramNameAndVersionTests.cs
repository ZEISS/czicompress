// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

/// <summary>
/// Tests for <see cref="ProgramNameAndVersion"/>.
/// </summary>
public class ProgramNameAndVersionTests
{
    private static readonly (int Major, int Minor) Version = (1, 2);

    [Fact]
    public void ToString_WhenCalled_ReturnsExpected()
    {
        // ACT
        var actual = new ProgramNameAndVersion().ToString();

        // ASSERT
        actual.Should().MatchRegex($@"^CZI Shrink {Version.Major}\.{Version.Minor}\.\d+(\+\d+)?$");
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
        actual.Should().MatchRegex($@"^{Version.Major}\.{Version.Minor}\.\d+(\+\d+)?$");
    }
}
