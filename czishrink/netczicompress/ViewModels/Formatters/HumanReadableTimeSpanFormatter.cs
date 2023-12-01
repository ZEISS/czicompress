// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels.Formatters;

using netczicompress.Models;

/// <summary>
/// A <see cref="TimeSpan"/> to string formatter that produces an opinionated
/// easily human-readable string.
/// <remarks>
/// Examples:
/// TimeSpan(days: 0, hours: 0, minutes:00, seconds: 30) : "30s"
/// TimeSpan(days: 0, hours: 0, minutes:40, seconds: 30) : "40m 30s"
/// TimeSpan(days: 0, hours: 3, minutes:03, seconds: 05) : "3h 3m 5s"
/// TimeSpan(days: 1, hours: 1, minutes:00, seconds: 02) : "25h 0m 2s".
/// </remarks>
/// </summary>
public class HumanReadableTimeSpanFormatter : ITimeSpanFormatter
{
    /// <inheritdoc cref="ITimeSpanFormatter"/>
    public string FormatTimeSpan(TimeSpan timeSpan)
    {
        var (_, _, totalHours, _, minutes, seconds, _) = timeSpan;
        var totalHoursTruncated = (int)totalHours;
        return
            $"{(totalHoursTruncated != 0 ? $"{totalHoursTruncated}h " : string.Empty)}" +
            $"{(minutes != 0 ? $"{minutes}m " : string.Empty)}" +
            $"{seconds}s";
    }
}
