// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompressTests.Models;

using System.IO.Abstractions;
using System.Reactive.Disposables;

/// <summary>
/// Unit tests for the <see cref="FileProcessingFailedHandler"/> class.
/// </summary>
public class FileProcessingFailedHandlerTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OnFileProcessingFailed_WithDifferentOptions_ReturnsCorrectValue(bool copyFailedFiles)
    {
        // Arrange
        string fileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var inputFileName = $"{fileName}.czi";
        using var deleteInput = DeleteLater(inputFileName);
        var inputFile = CreateFileInfo(inputFileName);
        var inputFileInfo = new FileInfo(inputFileName);
        using var inputStream = inputFileInfo.Create();
        inputStream.WriteByte(5);
        inputStream.Close();

        var outputFileName = $"{fileName}_out.czi";
        using var deleteOutput = DeleteLater(outputFileName);

        var tempOutputFileName = $"{fileName}_temp.czi";
        using var deleteTempOutput = DeleteLater(tempOutputFileName);

        var target = new FileProcessingFailedHandler();
        string innerException = "Something bad happened.";
        int reportedProgress = -1;

        // Act
        var result = target.FileProcessingFailed(
            inputFile,
            CreateFileInfo(outputFileName),
            CreateFileInfo(tempOutputFileName),
            progress => reportedProgress = progress,
            innerException,
            copyFailedFiles,
            CancellationToken.None);

        // Assert
        result.Should().Be(copyFailedFiles ? $"{innerException} {FileProcessingFailedHandler.SuccessfulCopyMessage}" : innerException);
        reportedProgress.Should().Be(copyFailedFiles ? 100 : -1);
    }

    [Fact]
    public void OnFileProcessingFailed_WithCopyOptionAndFailedCopyOperation_ReturnsCorrectValue()
    {
        // Arrange
        var target = new FileProcessingFailedHandler();
        var inFile = new Mock<IFileInfo>();
        inFile.Setup(x => x.CopyTo(It.IsAny<string>(), It.IsAny<bool>())).Throws<IOException>();
        var outFile = new Mock<IFileInfo>();
        var tempOutFile = new Mock<IFileInfo>();
        string innerException = "Something bad happened.";

        // Act
        var result = target.FileProcessingFailed(
            inFile.Object,
            outFile.Object,
            tempOutFile.Object,
            _ => { },
            innerException,
            true,
            CancellationToken.None);

        // Assert
        result.Should().Be($"{innerException} {FileProcessingFailedHandler.FailedCopyMessage}");
    }

    private static IFileInfo CreateFileInfo(string fileName)
    {
        var fileInfo = new Mock<IFileInfo>();
        fileInfo.Setup(x => x.FullName).Returns(fileName);
        return fileInfo.Object;
    }

    private static IDisposable DeleteLater(string path) => Disposable.Create(() => File.Delete(path));
}
