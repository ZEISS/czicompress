// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.IO.Abstractions;

/// <summary>
/// Tests for <see cref="CsvLogFileWriter"/>.
/// </summary>
public class CsvLogFileWriterTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FullRun_ProducesExpectedText(bool error)
    {
        // ARRANGE
        var data = new (string Path, long SizeIn, long SizeOut, TimeSpan? TimeElapsed, string? Error)[]
        {
            ("foo/bla/bla.czi", 100, 16, TimeSpan.Zero, null),
            ("foo/bla/blub.czi", 1000, 1600, TimeSpan.Zero, null),
            ("bar/bla/baz,; .CZI", 1000, 1600, null, "oops\nHere's another line."),
        };

        using var writer = new StringWriter();

        var sut = new CsvLogFileWriter(writer);

        var messages = from item in data
                       select new CompressorMessage.FileFinished(
                           Mock.Of<IFileInfo>(x => x.FullName == item.Path),
                           item.SizeIn,
                           item.SizeOut,
                           item.TimeElapsed,
                           item.Error);

        // ACT
        foreach (var item in messages)
        {
            sut.OnNext(item);
        }

        if (error)
        {
            sut.OnError(new Exception());
        }
        else
        {
            sut.OnCompleted();
        }

        // ASSERT
        var csvFileContents = writer.ToString();
        var csvFileRows = csvFileContents.Split("\r\n");

        csvFileRows.Should().BeEquivalentTo(
        new[]
        {
            "InputFile,SizeInput,SizeOutput,SizeRatio,SizeDelta,TimeToProcess,Status,ErrorMessage",
            "\"foo/bla/bla.czi\",100,16,0.16,-84,00:00:00:0000000,SUCCESS,",
            "\"foo/bla/blub.czi\",1000,1600,1.6,600,00:00:00:0000000,SUCCESS,",
            "\"bar/bla/baz,; .CZI\",1000,1600,,,,ERROR,\"oops\nHere's another line.\"",
            string.Empty,
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OnNext_WhenCalledAfterCompletion_ProducesNoResult(bool error)
    {
        // ARRANGE
        using var writer = new StringWriter();

        var sut = new CsvLogFileWriter(writer);

        if (error)
        {
            sut.OnError(new Exception());
        }
        else
        {
            sut.OnCompleted();
        }

        // ACT
        var message = new Fixture().Customize(new AutoMoqCustomization { ConfigureMembers = true }).Create<CompressorMessage.FileFinished>();
        sut.OnNext(message);

        // ASSERT
        var csvFileContents = writer.ToString();
        var csvFileRows = csvFileContents.Split("\r\n");

        csvFileRows.Should().BeEquivalentTo(
        new[]
        {
            "InputFile,SizeInput,SizeOutput,SizeRatio,SizeDelta,TimeToProcess,Status,ErrorMessage",
            string.Empty,
        });
    }
}
