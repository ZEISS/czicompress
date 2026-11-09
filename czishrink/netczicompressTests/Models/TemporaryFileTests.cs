// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.IO.Abstractions.TestingHelpers;

/// <summary>Tests exclusive staging and preservation of unrelated files.</summary>
public class TemporaryFileTests
{
    [Fact]
    public void AllocationAndCleanupPreserveLegacyNamesAndFinalDestination()
    {
        var fs = new MockFileSystem();
        fs.AddFile("destination.czi", new MockFileData("valuable final"));
        fs.AddFile("destination.czi~", new MockFileData("valuable tilde file"));
        fs.AddFile("destination.czi~1", new MockFileData("valuable numbered file"));
        using (var temporary = new TemporaryFile(fs.FileInfo.New("destination.czi")))
        {
            temporary.TemporaryFileCreationFailed.Should().BeFalse();
            temporary.Info.Exists.Should().BeFalse();
            temporary.Info.DirectoryName.Should().NotBe(fs.Path.GetFullPath("."));
            fs.File.WriteAllText(temporary.Info.FullName, "our partial output");
            temporary.DeleteAllOutFiles();
        }

        fs.File.ReadAllText("destination.czi").Should().Be("valuable final");
        fs.File.ReadAllText("destination.czi~").Should().Be("valuable tilde file");
        fs.File.ReadAllText("destination.czi~1").Should().Be("valuable numbered file");
        fs.AllFiles.Should().HaveCount(3);
        fs.AllDirectories.Where(path => fs.Path.GetFileName(path).StartsWith(".czishrink-", StringComparison.Ordinal)).Should().BeEmpty();
    }

    [Fact]
    public void SeparateAllocationsForTheSameDestinationUseSeparateWorkspaces()
    {
        var fs = new MockFileSystem();
        using var first = new TemporaryFile(fs.FileInfo.New("image.czi"));
        using var second = new TemporaryFile(fs.FileInfo.New("image.czi"));
        first.Info.DirectoryName.Should().NotBe(second.Info.DirectoryName);
        fs.File.WriteAllText(first.Info.FullName, "first partial");
        fs.File.WriteAllText(second.Info.FullName, "second partial");
        first.DeleteAllOutFiles();
        fs.File.ReadAllText(second.Info.FullName).Should().Be("second partial");
        second.DeleteAllOutFiles();
    }

    [Fact]
    public void MovingThenDisposingKeepsTheCompletedOutputAndReleasesStaging()
    {
        var fs = new MockFileSystem();
        string stagingPath;
        using (var temporary = new TemporaryFile(fs.FileInfo.New("image.czi")))
        {
            stagingPath = temporary.Info.DirectoryName!;
            fs.File.WriteAllText(temporary.Info.FullName, "complete data");
            temporary.MoveToOutFileIfExists();
        }

        fs.File.ReadAllText("image.czi").Should().Be("complete data");
        fs.Directory.Exists(stagingPath).Should().BeFalse();
        fs.AllFiles.Should().ContainSingle();
    }

    [Fact]
    public void DestinationAppearingBeforeMoveIsNeverOverwrittenOrDeleted()
    {
        var fs = new MockFileSystem();
        using var temporary = new TemporaryFile(fs.FileInfo.New("image.czi"));
        fs.File.WriteAllText(temporary.Info.FullName, "our partial");
        fs.File.WriteAllText("image.czi", "another writer's output");

        Action move = temporary.MoveToOutFileIfExists;
        move.Should().Throw<IOException>();
        temporary.DeleteAllOutFiles();

        fs.File.ReadAllText("image.czi").Should().Be("another writer's output");
    }

    [Fact]
    public void ExhaustedAllocationCannotDeleteTheUnownedFinalDestination()
    {
        var fs = new MockFileSystem();
        fs.AddFile("image.czi", new MockFileData("valuable original destination"));
        using var temporary = new TemporaryFile(fs.FileInfo.New("image.czi"), 0);
        temporary.TemporaryFileCreationFailed.Should().BeTrue();
        temporary.DeleteAllOutFiles();
        temporary.MoveToOutFileIfExists();
        fs.File.ReadAllText("image.czi").Should().Be("valuable original destination");
    }

    [Fact]
    public void ConstructorFailureReleasesItsReservationAndEmptyWorkspace()
    {
        var fs = new MockFileSystem();
        fs.AddFile("valuable.txt", new MockFileData("keep this unrelated file"));
        var destination = new Mock<System.IO.Abstractions.IFileInfo>();
        destination.SetupGet(file => file.FileSystem).Returns(fs);
        destination.SetupGet(file => file.DirectoryName).Returns(fs.Path.GetFullPath("."));
        destination.SetupGet(file => file.Name).Throws(new InvalidOperationException("Cannot read destination name"));

        Action allocate = () => new TemporaryFile(destination.Object);

        allocate.Should().Throw<InvalidOperationException>();
        fs.File.ReadAllText("valuable.txt").Should().Be("keep this unrelated file");
        fs.AllFiles.Should().ContainSingle();
        fs.AllDirectories.Where(path => fs.Path.GetFileName(path).StartsWith(".czishrink-", StringComparison.Ordinal)).Should().BeEmpty();
    }

    [Fact]
    public void DisposingNeverRecursivelyDeletesUnexpectedWorkspaceContents()
    {
        var fs = new MockFileSystem();
        using var temporary = new TemporaryFile(fs.FileInfo.New("image.czi"));
        string unexpected = fs.Path.Combine(temporary.Info.DirectoryName!, "unexpected.txt");
        fs.File.WriteAllText(unexpected, "keep this file");
        temporary.Dispose();
        fs.File.ReadAllText(unexpected).Should().Be("keep this file");
    }
}
