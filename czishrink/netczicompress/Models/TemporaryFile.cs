// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.IO.Abstractions;

/// <summary>
/// Reserves a private staging directory beside the destination. Existing files,
/// including legacy names ending in '~', are never reused or deleted.
/// </summary>
public sealed class TemporaryFile : IDisposable
{
    private readonly IFileInfo outFile;
    private readonly IDirectoryInfo? workspace;
    private readonly Stream? reservation;

    /// <summary>Initializes a new instance of the <see cref="TemporaryFile"/> class.</summary>
    /// <param name="outFile">The final destination file.</param>
    /// <param name="maxNumberOfTriesToCreate">The maximum reservation attempts.</param>
    public TemporaryFile(IFileInfo outFile, int maxNumberOfTriesToCreate = 100)
    {
        this.outFile = outFile;
        this.Info = outFile;
        var fs = outFile.FileSystem;
        for (int attempt = 0; attempt < maxNumberOfTriesToCreate; attempt++)
        {
            var directoryPath = fs.Path.Combine(outFile.DirectoryName!, $".czishrink-{Guid.NewGuid():N}");
            var reservationPath = directoryPath + ".lock";
            try
            {
                // CreateNew is atomic. DeleteOnClose releases only our reservation.
                this.reservation = fs.FileStream.New(reservationPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException) when (fs.File.Exists(reservationPath) || fs.Directory.Exists(reservationPath))
            {
                continue;
            }

            try
            {
                if (fs.Directory.Exists(directoryPath) || fs.File.Exists(directoryPath))
                {
                    this.reservation.Dispose();
                    this.reservation = null;
                    continue;
                }

                this.workspace = fs.Directory.CreateDirectory(directoryPath);
                this.Info = fs.FileInfo.New(fs.Path.Combine(directoryPath, outFile.Name));
                return;
            }
            catch
            {
                this.Dispose();
                this.reservation = null;
                throw;
            }
        }

        this.TemporaryFileCreationFailed = true;
    }

    /// <summary>Gets a value indicating whether a private staging directory could not be reserved.</summary>
    public bool TemporaryFileCreationFailed { get; }

    /// <summary>Gets the output path inside the owned staging directory.</summary>
    public IFileInfo Info { get; }

    /// <summary>Moves our completed output without overwriting a destination.</summary>
    public void MoveToOutFileIfExists()
    {
        this.Info.Refresh();
        if (!this.TemporaryFileCreationFailed && this.Info.Exists)
        {
            this.Info.MoveTo(this.outFile.FullName);
            this.Info.Refresh();
            this.outFile.Refresh();
        }
    }

    /// <summary>Deletes only the file owned by this operation.</summary>
    public void DeleteAllOutFiles()
    {
        if (this.reservation == null)
        {
            return;
        }

        try
        {
            this.Info.Refresh();
            if (this.Info.Exists)
            {
                this.Info.Delete();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A locked partial output can remain; never delete another path instead.
        }

        this.outFile.Refresh();
    }

    /// <summary>Releases staging space, leaving completed output intact.</summary>
    public void Dispose()
    {
        try
        {
            // Never recursively delete: unexpected contents must remain untouched.
            this.workspace?.Delete(recursive: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An unexpected or locked file must not be removed by cleanup.
        }
        finally
        {
            this.reservation?.Dispose();
        }
    }
}
