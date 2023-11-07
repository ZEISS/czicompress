// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;

/// <summary>
/// Cumulative statistics of a compression or decompression operation.
/// </summary>
public readonly record struct AggregateStatistics
{
    public static AggregateStatistics Empty { get; } = default;

    public int FilesWithErrors { get;  init; }

    public int FilesWithNoErrors { get; init; }

    public TimeSpan Duration { get; init; }

    public long InputBytes { get; init; }

    public long OutputBytes { get; init; }

    public long DeltaBytes => this.OutputBytes - this.InputBytes;

    public float OutputToInputRatio => 1.0f * this.OutputBytes / this.InputBytes;
}