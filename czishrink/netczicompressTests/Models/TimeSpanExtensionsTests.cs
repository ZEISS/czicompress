// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

/// <summary>
/// Tests for <see cref="TimeSpanExtensions"/>.
/// </summary>
public class TimeSpanExtensionsTests
{
    [Theory]
    [InlineData(12, 13, 14, 15, 0, "301:14:15:0000000")]
    [InlineData(0, 13, 14, 15, 0, "13:14:15:0000000")]
    [InlineData(0, 3, 14, 15, 0, "03:14:15:0000000")]
    [InlineData(0, 0, 04, 05, 10, "00:04:05:0000010")]
    [InlineData(10, 25, 62, 64, 10000, "266:03:14:0000000")]
    [InlineData(00, 00, 00, 00, int.MaxValue, "597:31:23:0000647")]
    public void ToDateTimeString_WhenCalled_ReturnsExpected(int days, int hours, int minutes, int seconds, int milliseconds, string resultantString)
    {
        // PREPARE
        var timeSpan = new TimeSpan(days, hours, minutes, seconds, milliseconds);

        // ACT
        var actual = timeSpan.ToDateTimeString();

        // ASSERT
        actual.Should().Be(resultantString);
    }
}
