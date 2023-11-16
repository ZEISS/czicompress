// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.ComponentModel.DataAnnotations;

/// <summary>
/// Defines Zstd compression levels.
/// </summary>
public record CompressionLevel
{
    private readonly int internalValue = DefaultValue;

    public static readonly CompressionLevel Default;

    static CompressionLevel()
    {
        Default = new CompressionLevel() { Value = DefaultValue };
    }

    /// <summary>
    /// Gets the minimum allowed compression level.
    /// </summary>
    /// <remarks>In this application the value is 0, though Zstd would allow values down to -131072.</remarks>
    public static int Minimum { get; } = 0;

    /// <summary>
    /// Gets maximum allowed compression level.
    /// </summary>
    /// <remarks>The value of this property is 22.</remarks>
    public static int Maximum { get; } = 22;

    /// <summary>
    /// Gets default compression level if not specified.
    /// </summary>
    /// <remarks>The value of this property is 1.</remarks>
    public static int DefaultValue { get; } = 1;

    [Range(0, 22)]
    public required int Value
    {
        get => this.internalValue;
        init =>
            this.internalValue = value > Maximum || value < Minimum
                ? throw new ArgumentOutOfRangeException(
                    paramName: nameof(value),
                    message: $"{nameof(value)} must be between {Minimum} and {Maximum}, not {value}")
                : value;
    }
}