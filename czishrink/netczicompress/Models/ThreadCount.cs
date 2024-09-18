// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

public partial record ThreadCount
{
    public static readonly ThreadCount Default;

    private readonly int internalValue = Maximum;

    static ThreadCount()
    {
        DefaultValue = Maximum;
        Default = new ThreadCount { Value = DefaultValue };
    }

    public static int Maximum { get; } = GetMaxProcessorCount();

    public static int Minimum { get; } = 1;

    /// <summary>
    /// Gets default thread count if not specified.
    /// </summary>
    public static int DefaultValue { get; } = Maximum;

    public required int Value
    {
        get => this.internalValue;
        init =>
            this.internalValue = value > Maximum || value < Minimum
                ? throw new ArgumentOutOfRangeException(
                    paramName: nameof(value),
                    $"{nameof(value)} must be between {Minimum} and {Maximum}, not {value}")
                : value;
    }

    // Note we leave one processor free for the UI thread.
    private static int GetMaxProcessorCount()
    {
        return Environment.ProcessorCount > 4
            ? Environment.ProcessorCount - 1
            : Environment.ProcessorCount;
    }
}