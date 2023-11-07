// SPDX-FileCopyrightText: 2023 Carl Zeiss Microscopy GmbH
//
// SPDX-License-Identifier: GPL-3.0-or-later

namespace netczicompress.Models;

using System.Diagnostics;
using System.IO.Abstractions;

/// <summary>
/// Launches a file with its associated app,
/// or displays a file in its folder with the operating system's file manager.
/// </summary>
public class FileLauncher : IFileLauncher
{
    // Cf. https://github.com/dotnet/runtime/blob/52806bc157decf345005249c9ea7969c8b9e7e1b/src/libraries/System.Private.CoreLib/src/System/OperatingSystem.cs#L14C9-L41C6
    private const string Windows = "WINDOWS";
    private const string Osx = "OSX";
    private const string Linux = "LINUX";
    private const string FreeBsd = "FREEBSD";
    private const string NetBsd = "NETBSD";
    private const string Solaris = "SOLARIS";

    private const string Unsupported = "UNSUPPORTED";
    private static readonly string[] SupportedPlatforms =
        {
            Windows,
            Osx,
            Linux,
            FreeBsd,
            NetBsd,
            Solaris,
        };

    private readonly IFileSystem fs;
    private readonly Func<string, string?> getEnvironmentVariable;

    public FileLauncher(IFileSystem fs, Func<string, string?> getEnvironmentVariable)
    {
        this.fs = fs;
        this.getEnvironmentVariable = getEnvironmentVariable;
    }

    protected string CurrentPlatform { get; init; } = GetPlatform();

    public Task Launch(string path)
    {
        return this.RunAsync(GetLaunchStartInfo(path));
    }

    // Adapted from https://stackoverflow.com/a/73409251
    public async Task Reveal(string path)
    {
        var startInfo = this.GetRevealStartInfo(path);
        var exitCode = await this.RunAsync(startInfo);

        if (exitCode != 0 && startInfo.FileName == "dbus-send")
        {
            // dbus-send was unsuccessful
            await this.RunAsync(this.GetOpenParentFolderStartInfo(path));
        }
    }

    protected virtual async Task<int> RunAsync(ProcessStartInfo processStartInfo)
    {
        using var process = new Process { StartInfo = processStartInfo };
        process.Start();
        await process.WaitForExitAsync();
        return process.ExitCode;
    }

    protected virtual string PathToFileUri(string path)
    {
        return new Uri(path).AbsoluteUri;
    }

    private static string GetPlatform()
    {
        return SupportedPlatforms.FirstOrDefault(x => OperatingSystem.IsOSPlatform(x)) ?? Unsupported;
    }

    private static ProcessStartInfo GetRevealStartInfoOsx(string path)
    {
        return new ProcessStartInfo
        {
            FileName = "explorer",
            Arguments = $"-R \"{path}\"",
        };
    }

    private static ProcessStartInfo GetLaunchStartInfo(string? path)
    {
        return new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
        };
    }

    private ProcessStartInfo GetRevealStartInfoWindows(string path)
    {
        var fileName = "explorer";
        var sysRoot = this.getEnvironmentVariable("SYSTEMROOT");
        if (sysRoot != null)
        {
            fileName = this.fs.Path.Combine(sysRoot, fileName);
        }

        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = $"/select,\"{path}\"",
        };
    }

    private ProcessStartInfo GetRevealStartInfoDBus(string path)
    {
        string fileUri = this.PathToFileUri(path);
        return new ProcessStartInfo()
        {
            FileName = "dbus-send",
            Arguments = $"--print-reply --dest=org.freedesktop.FileManager1 /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:\"{fileUri}\" string:\"\"",
            UseShellExecute = true,
        };
    }

    private ProcessStartInfo GetOpenParentFolderStartInfo(string path)
    {
        return GetLaunchStartInfo(this.fs.Path.GetDirectoryName(path));
    }

    private ProcessStartInfo GetRevealStartInfo(string path)
    {
        Func<string, ProcessStartInfo> core = this.CurrentPlatform switch
        {
            Windows => this.GetRevealStartInfoWindows,
            Osx => GetRevealStartInfoOsx,
            Unsupported => this.GetOpenParentFolderStartInfo,
            _ => this.GetRevealStartInfoDBus,
        };

        return core(path);
    }
}
