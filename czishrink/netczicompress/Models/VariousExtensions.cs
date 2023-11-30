// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

public static class VariousExtensions
{
    public static async IAsyncEnumerable<IFileInfo> EnumerateFilesAsync(
        this IDirectoryInfo folder,
        string searchPattern,
        EnumerationOptions opts,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var files = await Task.Run(() => folder.EnumerateFiles(searchPattern, opts)).ConfigureAwait(false);
        using var e = files.GetEnumerator();
        while (await Task.Run(() => e.MoveNext()).ConfigureAwait(false))
        {
            if (token.IsCancellationRequested)
            {
                yield break;
            }

            yield return e.Current;
        }
    }

    public static AggregateStatistics AddData(this AggregateStatistics aggregateStatistics, CompressorMessage.FileFinished msg, TimeSpan elapsed)
    {
        if (msg.ErrorMessage == null)
        {
            return aggregateStatistics with
            {
                FilesWithNoErrors = aggregateStatistics.FilesWithNoErrors + 1,
                InputBytes = aggregateStatistics.InputBytes + msg.SizeInput,
                OutputBytes = aggregateStatistics.OutputBytes + msg.SizeOutput,
                Duration = elapsed,
            };
        }
        else
        {
            return aggregateStatistics with
            {
                FilesWithErrors = aggregateStatistics.FilesWithErrors + 1,
                Duration = elapsed,
            };
        }
    }

    public static void Deconstruct(
        this TimeSpan value,
        out double totalDays,
        out int days,
        out double totalHours,
        out int hours,
        out int minutes,
        out int seconds,
        out int milliseconds)
    {
        totalDays = value.TotalDays;
        days = value.Days;
        totalHours = value.TotalHours;
        hours = value.Hours;
        minutes = value.Minutes;
        seconds = value.Seconds;
        milliseconds = value.Milliseconds;
    }
}
