// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

/// <summary>
/// Tests for <see cref="ThreadCount"/>.
/// </summary>
public class ThreadCountTests
{
    [Fact]
    public void DefaultConstructor_HasDefaultValue()
    {
        var sut = new ThreadCount();
        sut.Value.Should().Be(ThreadCount.DefaultValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(230)]
    public void OutOfRange_ShouldThrowOutOfRangeException(int value)
    {
        var act = () => _ = new ThreadCount { Value = value };

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1)]
    public void InRangeValue_ShouldBeEqual(int value)
    {
        var sut = new ThreadCount() { Value = value };
        sut.Value.Should().Be(value);
    }
}