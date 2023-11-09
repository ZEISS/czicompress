// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.IO.Abstractions;

/// <summary>
/// Creates and handles temporary files according to the following logic:
/// <list type="bullet">
/// <item> The target filename "outfile_name" is given.</item>
/// <item> The basic temporary filename is "outfile_name" + "~".</item>
/// <item> If the basic temporary filename can be used (file does not yet exist or can be deleted)
///        then it will be used.</item>
/// <item> If it cannot be used, we (now recursively) try with another file name "outfile_name" + "~" + i,
///        with i being an index starting at 1.</item>
/// <item> There is a maximum number of tries, so the index is bounded by "maximum number of tries - 1".</item>
/// </list>
/// </summary>
public class TemporaryFile
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Models.TemporaryFile"/> class.
    ///
    /// The <see cref="OutFile"/> is set and <see cref="Info"/> (not the file on the hard disk)
    /// immediately created, if possible.
    /// By this <see cref="TemporaryFileCreationFailed"/> is set accordingly.
    /// </summary>
    /// <param name="outfile">The basic output file name.</param>
    /// <param name="maxNumberOfTriesToCreate">The maximum number or tries.</param>
    public TemporaryFile(
        IFileInfo outfile,
        int maxNumberOfTriesToCreate = 100)
    {
        this.OutFile = outfile;
        this.MaxNumberOfTriesToCreate = maxNumberOfTriesToCreate;
        this.Info = this.TryCreate();
    }

    /// <summary>
    /// Gets a value indicating whether the temporary file creation failed.
    /// </summary>
    public bool TemporaryFileCreationFailed { get; private set; }

    /// <summary>
    /// Gets the created temporary file.
    /// </summary>
    public IFileInfo Info { get; }

    /// <summary>
    /// Gets the created final output file.
    /// </summary>
    protected IFileInfo OutFile { get; }

    /// <summary>
    /// Gets the maximum number of tries for the temporary file creation.
    /// </summary>
    private int MaxNumberOfTriesToCreate { get; }

    /// <summary>
    /// If the temporary file exists, moves the temporary file to the final output file.
    /// Mind that the <see cref="IFileSystemInfo.FullName"/> attribute of the temporary file will
    /// be adapted not the <see cref="IFileSystemInfo.Exists"/> flag.
    /// </summary>
    public void MoveToOutFileIfExists()
    {
        this.Info.Refresh(); // mind that the file might have not existed when the variable was created.
        if (!this.Info.Exists)
        {
            return;
        }

        this.Info.MoveTo(this.OutFile.FullName);
        this.Info.Refresh(); // mind that the move changes the full name, not the `Exits` flag.
        this.OutFile.Refresh();
    }

    /// <summary>
    /// Deletes the basic output file and the temporary file.
    /// </summary>
    public void DeleteAllOutFiles()
    {
        _ = this.TryDelete(this.OutFile);
        _ = this.TryDelete(this.Info);
    }

    /// <summary>
    /// Tries to create the temporary file.
    /// <list type="bullet">
    /// <item> Create the temporary filename.</item>
    /// <item> If it can be used, return it.</item>
    /// <item> If not, append an increasing index until the file can be deleted or used.</item>
    /// <item> If the maximum number of tries is reached, set "failed" and return the latest try.</item>
    /// </list>
    /// </summary>
    /// <returns>The temporary file info.</returns>
    private IFileInfo TryCreate()
    {
        var outFileName = this.OutFile.FullName;
        var tempOutFileName = this.GetBasicTempFileName(outFileName);
        IFileInfo temporaryFile = this.OutFile.FileSystem.FileInfo.New(tempOutFileName);
        var postfixIndex = 1;
        while (temporaryFile.Exists && !this.TryDelete(temporaryFile))
        {
            if (postfixIndex + 1 > this.MaxNumberOfTriesToCreate)
            {
                this.TemporaryFileCreationFailed = true;
                return temporaryFile;
            }

            temporaryFile = this.CreateWithIndex(this.OutFile, tempOutFileName, postfixIndex);
            ++postfixIndex;
        }

        this.TemporaryFileCreationFailed = false;
        return temporaryFile;
    }

    private bool TryDelete(IFileInfo file)
    {
        try
        {
            file.Refresh();
            if (file.Exists)
            {
                file.Delete();
                file.Refresh();
            }
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    private string GetBasicTempFileName(string outFileName)
    {
        return outFileName + "~";
    }

    private IFileInfo CreateWithIndex(IFileInfo outFile, string tempOutFileName, int index)
    {
        return outFile.FileSystem.FileInfo.New($"{tempOutFileName}{index}");
    }
}
