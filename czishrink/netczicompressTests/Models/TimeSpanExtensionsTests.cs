// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

/// <summary>
/// Tests for <see cref="TimeSpan"/> extensions in <see cref="VariousExtensions"/>.
/// </summary>
public class TimeSpanExtensionsTests
{
    [Theory]
    [AutoData]
    public void Deconstruct_HasExpectedValues(int days, int hours, int minutes, int seconds, int milliseconds)
    {
        // PREPARE
        var value = new TimeSpan(days, hours, minutes, seconds, milliseconds);

        // ACT
        var (deconstructedTotalDays, deconstructedDays, deconstructedTotalHours, deconstructedHours, deconstructedMinutes, deconstructedSeconds, deconstructedMilliseconds) = value;

        // ASSERT
        deconstructedTotalDays.Should().Be(value.TotalDays);
        deconstructedDays.Should().Be(value.Days);
        deconstructedTotalHours.Should().Be(value.TotalHours);
        deconstructedHours.Should().Be(value.Hours);
        deconstructedMinutes.Should().Be(value.Minutes);
        deconstructedSeconds.Should().Be(value.Seconds);
        deconstructedMilliseconds.Should().Be(value.Milliseconds);
    }
}