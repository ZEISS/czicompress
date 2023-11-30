// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System;
using System.Diagnostics;
using System.IO;

using static System.FormattableString;

/// <summary>
/// Writes a sequence of <see cref="CompressorMessage.FileFinished"/> messages to a CSV file.
/// </summary>
public class CsvLogFileWriter : IObserver<CompressorMessage.FileFinished>
{
    private bool isCompleted;

    public CsvLogFileWriter(TextWriter writer)
    {
        this.Writer = writer;
        this.WriteHeader();
    }

    public TextWriter Writer { get; }

    public void OnCompleted()
    {
        lock (this.Writer)
        {
            this.isCompleted = true;
            this.Writer.Flush();
            this.Writer.Close();
        }
    }

    public void OnError(Exception error)
    {
        this.OnCompleted();
    }

    public void OnNext(CompressorMessage.FileFinished value)
    {
        this.Write(ToLine(value));
    }

    private static string ToLine(CompressorMessage.FileFinished value)
    {
        return Invariant(
                $"{
                    Quote(value.InputFile.FullName)},{
                    value.SizeInput},{
                    value.SizeOutput},{
                    (value.ErrorMessage == null ? value.SizeRatio : string.Empty)},{
                    (value.ErrorMessage == null ? value.SizeDelta : string.Empty)},{
                    value.TimeElapsed?.ToDateTimeString() ?? string.Empty},{
                    (value.ErrorMessage == null ? "SUCCESS" : "ERROR")},{
                    Quote(value.ErrorMessage)}")
                + "\r\n";
    }

    private static string Quote(string? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        return '"' + value.Replace("\"", "\"\"") + '"';
    }

    private void WriteHeader()
    {
        this.Write("InputFile,SizeInput,SizeOutput,SizeRatio,SizeDelta,TimeToProcess,Status,ErrorMessage\r\n");
    }

    private void Write(string line)
    {
        lock (this.Writer)
        {
            if (this.isCompleted)
            {
                Debug.WriteLine($"{nameof(CsvLogFileWriter)}: received a message after completion...");
                return;
            }

            this.Writer.Write(line);
        }
    }
}
