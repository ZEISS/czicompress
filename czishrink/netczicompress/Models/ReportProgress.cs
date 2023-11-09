// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

/// <summary>
/// Delegate that is invoked by processing function to provide updates.
/// </summary>
/// <param name="percentComplete">The percentage of completion in the range 0 .. 100.</param>
public delegate void ReportProgress(int percentComplete);
