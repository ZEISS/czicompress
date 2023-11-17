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
        // Examples for match:
        // CZI Shrink 1.0.2+6897436059.7c198c4469d3ed084d7f213cf8500a55062bb727
        // CZI Shrink 1.0.2+6897436059
        // CZI Shrink 1.0.2
        // Pattern: MAJOR.MINOR.PATCH+BUILD.COMMITSHA
        // Based on https://semver.org/spec/v2.0.0.html#spec-item-10
        actual.Should().MatchRegex(@"^CZI Shrink 1\.0\.\d+(\+\d+(\.[a-z0-9]+)?)?$");
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
        // Examples for match:
        // CZI Shrink 1.0.2+6897436059.7c198c4469d3ed084d7f213cf8500a55062bb727
        // CZI Shrink 1.0.2+6897436059
        // CZI Shrink 1.0.2
        // Pattern: MAJOR.MINOR.PATCH+BUILD.COMMITSHA
        // Based on https://semver.org/spec/v2.0.0.html#spec-item-10
        actual.Should().MatchRegex(@"^1\.0\.\d+(\+\d+(\.[a-z0-9]+)?)?$");
    }
}
