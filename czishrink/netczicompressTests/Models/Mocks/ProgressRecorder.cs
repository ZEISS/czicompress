// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models.Mocks;

using System.Collections.Concurrent;

/// <summary>
/// Records all progress messages from an <see cref="IFolderCompressor"/> run.
/// </summary>
internal class ProgressRecorder : IObserver<CompressorMessage.FileStarting>
{
    public ConcurrentDictionary<string, List<int>> ProgressValuesByFile { get; } = new();

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void OnNext(CompressorMessage.FileStarting value)
    {
        this.RecordProgress(value);
    }

    private void RecordProgressValue(string file, int p)
    {
        var progressValuesForFile = this.ProgressValuesByFile.GetOrAdd(file, new List<int>());
        progressValuesForFile.Add(p);
    }

    private void RecordProgress(CompressorMessage.FileStarting fileStarting)
    {
        fileStarting.ProgressPercent.Subscribe(
            p => this.RecordProgressValue(fileStarting.InputFile.FullName, p));
    }
}
