// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;

/// <summary>
/// Extensions for <see cref="TimeSpan"/>.
/// </summary>
public static class TimeSpanExtensions
    {
    /// <summary>
    /// Formats the TimeSpan to a string similar to the common formatting scheme where instead of hours we use <see cref="TimeSpan.TotalHours"/>.
    /// </summary>
    /// <param name="value">TimeSpan to extend.</param>
    /// <returns>Formatted date with TotalHours instead of Hours.</returns>
    /// <remarks>
    /// This is to solve the issue with <see cref="TimeSpan"/> that are longer than 24H. See https://stackoverflow.com/questions/39821991/how-to-display-hours-and-minutes-from-timespan-with-more-than-24-hours.
    /// </remarks>
    public static string ToDateTimeString(this TimeSpan value)
        {
        return string.Format(
            "{0:00}:{1:00}:{2:00}:{3:0000000}",
            value.TotalHours,
            value.Minutes,
            value.Seconds,
            value.Milliseconds);
    }
    }
