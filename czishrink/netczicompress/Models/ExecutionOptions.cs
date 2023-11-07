// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

/// <summary>
/// Execution options to apply to operations.
/// </summary>
/// <param name="ThreadCount">The maximum number of threads to use.</param>
public record ExecutionOptions(ThreadCount ThreadCount);