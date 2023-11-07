// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

/// <summary>
/// Tests for <see cref="TemporaryFile"/>.
/// </summary>
public class TemporaryFileTests
{
    [Fact]
    public void WhenNoOutputFileExist_CreationOfTemporaryOutputDirSucceeds()
    {
        // ARRANGE
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
        var outFile = fileSystem.FileInfo.New("outFile");

        // ACT
        var sut = new TemporaryFileOverridenDelete(outFile);

        // ASSERT
        var expectedOutFileName = fileSystem.Path.GetFullPath("outFile");
        var expectedTempOutFileName = expectedOutFileName + "~";
        sut.GetFullOutFileName().Should().Be(expectedOutFileName);
        sut.TemporaryFileCreationFailed.Should().BeFalse();
        sut.Info.FullName.Should().Be(expectedTempOutFileName);
        sut.Info.Exists.Should().BeFalse();
    }

    [Fact]
    public void WhenNoOutputFileExistAndTemporaryFilesAreWritten_TemporaryFileShouldExistAndOutputFileNotYet()
    {
        // ARRANGE
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
        var outFile = fileSystem.FileInfo.New("outFile");

        // ACT
        var sut = new TemporaryFileOverridenDelete(outFile);
        sut.Info.Create(); // simulates the compression processing

        // ASSERT
        sut.GetOutputFile().Exists.Should().BeFalse();
        sut.MoveToOutFileIfExists();
        sut.GetOutputFile().Exists.Should().BeTrue();
    }

    [Fact]
    public void WhenNoOutputFileExistAndTemporaryFilesAreWrittenAndMoved_OutFileShouldExistAndTemporaryFileNotMore()
    {
        // ARRANGE
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>());
        var outFile = fileSystem.FileInfo.New("outFile");

        // ACT
        var sut = new TemporaryFileOverridenDelete(outFile);
        sut.Info.Create(); // simulates the compression processing
        sut.MoveToOutFileIfExists(); // simulates the compression processing

        // ASSERT
        var expectedTempOutFileName = fileSystem.Path.GetFullPath("outFile") + "~";

        // the sut's temporary output file will be renamed upon the move,
        // so we need to create a new file info from the original file name.
        var tempOutFile = fileSystem.FileInfo.New(expectedTempOutFileName);
        tempOutFile.Exists.Should().BeFalse();
        sut.DeleteAllOutFiles();
        sut.GetOutputFile().Exists.Should().BeFalse();
    }

    [Fact]
    public void WhenOutputFileExistsAndCanBeDeleted_CreationOfTemporaryOutputFileSucceeds()
    {
        // ARRANGE
        var fileSystem = CreateFileSystem();
        var outFile = fileSystem.FileInfo.New("outFile");

        // ACT
        var sut = new TemporaryFileOverridenDelete(outFile);

        // ASSERT
        var expectedOutFileName = fileSystem.Path.GetFullPath("outFile");
        var expectedTempOutFileName = expectedOutFileName + "~";
        sut.GetFullOutFileName().Should().Be(expectedOutFileName);
        sut.TemporaryFileCreationFailed.Should().BeFalse();
        sut.Info.FullName.Should().Be(expectedTempOutFileName);
        sut.Info.Exists.Should().BeFalse();
    }

    [Fact]
    public void WhenFourTemporaryOutputFileExistAndCannotBeDeleted_CreationOfTemporaryOutputFileSucceeds()
    {
        // ARRANGE
        var fileSystem = CreateFileSystem(readOnlyLevel: 2);
        var outFile = fileSystem.FileInfo.New("outFile");

        // ACT
        var sut = new TemporaryFileOverridenDelete(outFile);

        // ASSERT
        var expectedOutFileName = fileSystem.Path.GetFullPath("outFile");
        var expectedTempOutFileName = expectedOutFileName + "~4";
        sut.GetFullOutFileName().Should().Be(expectedOutFileName);
        sut.TemporaryFileCreationFailed.Should().BeFalse();
        sut.Info.FullName.Should().Be(expectedTempOutFileName);
        sut.Info.Exists.Should().BeFalse();
    }

    [Fact]
    public void WhenFourTemporaryOutputFileExistAndCannotBeDeletedAfterMove_OutputFileShouldExistAndTemporaryFileNoMote()
    {
        // ARRANGE
        var fileSystem = CreateFileSystem(readOnlyLevel: 2);
        var outFile = fileSystem.FileInfo.New("outFile");

        // ACT
        var sut = new TemporaryFileOverridenDelete(outFile);
        sut.Info.Create(); // simulates the compression processing
        var tempOutFileName = sut.Info.FullName;
        sut.MoveToOutFileIfExists();

        // ASSERT
        sut.GetOutputFile().Exists.Should().BeTrue();

        // the sut's temporary output file will be renamed upon the move,
        // so we need to create a new file info from the original file name.
        fileSystem.FileInfo.New(tempOutFileName).Exists.Should().BeFalse();
    }

    [Fact]
    public void WhenFourTemporaryOutputFileExistAndTwoCannotBeDeleted_CreationOfTemporaryOutputFileSucceeds()
    {
        // ARRANGE
        var fileSystem = CreateFileSystem(readOnlyLevel: 1);
        var outFile = fileSystem.FileInfo.New("outFile");

        // ACT
        var sut = new TemporaryFileOverridenDelete(outFile);

        // ASSERT
        var expectedOutFileName = fileSystem.Path.GetFullPath("outFile");
        var expectedTempOutFileName = expectedOutFileName + "~2";
        sut.GetFullOutFileName().Should().Be(expectedOutFileName);
        sut.TemporaryFileCreationFailed.Should().BeFalse();
        sut.Info.FullName.Should().Be(expectedTempOutFileName);
        sut.Info.Exists.Should().BeFalse();
    }

    [Fact]
    public void WhenFourTemporaryOutputFileExistAndTwoCannotBeDeletedAfterMove_OutputFileShouldExistAndTemporaryFileNoMote()
    {
        // ARRANGE
        var fileSystem = CreateFileSystem(readOnlyLevel: 1);
        var outFile = fileSystem.FileInfo.New("outFile");

        // ACT
        var sut = new TemporaryFileOverridenDelete(outFile);
        sut.Info.Create(); // simulates the compression processing
        var tempOutFileName = sut.Info.FullName;
        sut.MoveToOutFileIfExists();

        // ASSERT
        sut.GetOutputFile().Exists.Should().BeTrue();
        fileSystem.FileInfo.New(tempOutFileName).Exists.Should().BeFalse();
    }

    [Fact]
    public void WhenMaximumTriesToCreateTemporaryFileAreReached_CreationOfTemporaryOutputFileFails()
    {
        // ARRANGE
        var fileSystem = CreateFileSystem(readOnlyLevel: 2);
        var outFile = fileSystem.FileInfo.New("outFile");

        // ACT
        var sut = new TemporaryFileOverridenDelete(outFile, 4);

        // ASSERT
        sut.TemporaryFileCreationFailed.Should().BeTrue();
    }

    private static MockFileSystem CreateFileSystem(uint readOnlyLevel = 0)
    {
        // var outFileName = "outFile";
        var tempOutFile = "outFile~";
        var tempOutFile1 = "outFile~1";
        var tempOutFile2 = "outFile~2";
        var tempOutFile3 = "outFile~3";

        // var outFileMock = new MockFileData("this is the outfile.");
        var tempOutFileMock = new MockFileData("this is the temporary outfile.");
        var tempOutFileMock1 = new MockFileData("this is the temporary outfile 1.");
        var tempOutFileMock2 = new MockFileData("this is the temporary outfile 2.");
        var tempOutFileMock3 = new MockFileData("this is the temporary outfile 3.");

        if (readOnlyLevel >= 1)
        {
            tempOutFileMock.Attributes = FileAttributes.ReadOnly;
            tempOutFileMock1.Attributes = FileAttributes.ReadOnly;
        }

        if (readOnlyLevel >= 2)
        {
            tempOutFileMock2.Attributes = FileAttributes.ReadOnly;
            tempOutFileMock3.Attributes = FileAttributes.ReadOnly;
        }

        var files = new Dictionary<string, MockFileData>
        {
            { tempOutFile, tempOutFileMock },
            { tempOutFile1, tempOutFileMock1 },
            { tempOutFile2, tempOutFileMock2 },
            { tempOutFile3, tempOutFileMock3 },
        };

        var result = new MockFileSystem(files);
        return result;
    }

    private class TemporaryFileOverridenDelete : TemporaryFile
    {
        public TemporaryFileOverridenDelete(IFileInfo outfile, int maxNumberOfTriesToCreate = 5)
            : base(outfile, maxNumberOfTriesToCreate)
        {
        }

        public IFileInfo GetOutputFile()
        {
            return this.OutFile;
        }

        public string GetFullOutFileName()
        {
            return this.OutFile.FullName;
        }
    }
}