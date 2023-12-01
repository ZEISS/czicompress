// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.ViewModels.Formatters;

/// <summary>
/// A <see cref="TimeSpan"/> to <see cref="string"/> formatter.
/// </summary>
/// <remarks>
/// This formatting is meant to be more complex than
/// what can be achieved with a format string passed to
/// string.Format or TimeSpan.ToString.
/// </remarks>
public interface ITimeSpanFormatter
{
    /// <summary>
    /// Produce a formatted string from a <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="timeSpan"><see cref="TimeSpan"/> to produce a string from.</param>
    /// <returns>Formatted string representing <see cref="TimeSpan"/>.</returns>
    public string FormatTimeSpan(TimeSpan timeSpan);
}
