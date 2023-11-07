// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

/// <summary>
/// Tests for <see cref="CompressionLevel"/>.
/// </summary>
public class CompressionLevelTests
{
    [Fact]
    public void DefaultConstructor_HasDefaultValue()
    {
        var sut = new CompressionLevel();
        sut.Value.Should().Be(CompressionLevel.DefaultValue);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(23)]
    [InlineData(-100)]
    [InlineData(230)]
    public void OutOfRange_ShouldThrowOutOfRangeException(int value)
    {
        var act = () =>
        {
            var sut = new CompressionLevel() { Value = value };
        };

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(22)]
    [InlineData(9)]
    [InlineData(0)]
    public void InRangeValue_ShouldBeEqual(int value)
    {
        var sut = new CompressionLevel() { Value = value };
        sut.Value.Should().Be(value);
    }
}