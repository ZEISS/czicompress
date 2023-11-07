// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.IO.Abstractions;

/// <summary>
/// A parameter-object that contains the parameters for <see cref="IFolderCompressor.PrepareNewRun"/>.
/// </summary>
/// <param name="InputDir">The input directory.</param>
/// <param name="OutputDir">The output directory.</param>
/// <param name="Recursive">True if the input directory should be scanned recursively, else false.</param>
/// <param name="Mode">The compression mode.</param>
/// <param name="ExecutionOptions">Execution options to apply to operations.</param>
/// <param name="ProcessingOptions">Additional processing options.</param>
public record FolderCompressorParameters(
    IDirectoryInfo InputDir,
    IDirectoryInfo OutputDir,
    bool Recursive,
    CompressionMode Mode,
    ExecutionOptions ExecutionOptions,
    ProcessingOptions ProcessingOptions);