// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.ViewModels.Formatters;

using netczicompress.ViewModels.Formatters;

/// <summary>
/// Tests for <see cref="HumanReadableTimeSpanFormatter"/>.
/// </summary>
public class HumanReadableTimeSpanFormatterTests
    {
    [Theory]
    [InlineData(12, 13, 14, 15, 0, "301h 14m 15s")]
    [InlineData(0, 13, 14, 15, 0, "13h 14m 15s")]
    [InlineData(0, 3, 14, 15, 0, "3h 14m 15s")]
    [InlineData(0, 0, 04, 05, 10, "4m 5s")]
    [InlineData(10, 25, 62, 64, 10000, "266h 3m 14s")]
    [InlineData(00, 00, 00, 00, int.MaxValue, "596h 31m 23s")]
    [InlineData(00, 256204778, 48, 5, 477, "256204778h 48m 5s")] /* roughly equivalent to TimeSpan.MaxValue */
    [InlineData(0, -13, -14, -15, 0, "-13h -14m -15s")] /* Should be impossible but it doesn't hurt to handle */
    public void FormatTimeSpan_CreatesExpectedString(int days, int hours, int minutes, int seconds, int milliseconds, string formattedString)
    {
        // ARRANGE
        var sut = new HumanReadableTimeSpanFormatter();

        var input = new TimeSpan(days, hours, minutes, seconds, milliseconds);

        // ACT
        var producedString = sut.FormatTimeSpan(input);

        // ASSERT
        producedString.Should().BeEquivalentTo(formattedString);
    }
}
