// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models.Mocks;

public class Recorder<T> : IObserver<T>
{
    public List<T> Data { get; } = new();

    public bool Completed { get; private set; }

    public Exception? Error { get; private set; }

    public void OnCompleted()
    {
        this.Completed = true;
    }

    public void OnError(Exception error)
    {
        this.Error = error;
    }

    public void OnNext(T value)
    {
        if (this.Completed || this.Error != null)
        {
            throw new InvalidOperationException();
        }

        this.Data.Add(value);
    }
}
