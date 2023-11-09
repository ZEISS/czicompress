# CziShrink
An open source and cross-platform GUI for [CziCompress](https://github.com/ZEISS/czicompress), made with Avalonia UI and .NET.

## Table of contents

 - [Overview](#overview)
     - [Download](#download)
     - [System Requirements](#system-requirements)
     - [Theme Support](#theme-support)
     - [I/O](#io)
     - [Parameters](#parameters)
         - [Operations](#operations)
     - [Progress Update](#progress-update)
     - [Compression Statistics](#compression-statistics)
         - [Sharing](#sharing)
     - [Errors](#errors)
 - [Logs](#logs)
 - [FAQ](#faq)
 - [Known Issues](#known-issues)
 - [Potential Future Enhancements](#potential-future-enhancements)
 - [Feedback](#feedback)

## Overview

## System Requirements

### Windows
 - Windows 10 or later
### Linux
 - Supported OS:
     - Debian 9 (Stretch) and higher
     - Ubuntu 16.04 and higher
 - X11
 - Clipboad functionality requires xclip to be installed

## Theme Support
Dark and a light themes are set automatically based on your OS settings.
There is currently no way to manually set this.

![Light Theme](.images/CziShrink_LightTheme.png)![Dark Theme](.images/CziShrink_DarkTheme.png)

## I/O

Input and output folders are selected, and the tool will operate on all CZI-documents within the input folder.
The intent of the tool is to be a bulk-compression utility.

## Parameters

### Operations

| Operation | Description |
|-----------|-------------|
| Compress uncompressed data                    |Compresses only uncompresed subblocks and copies others.|
| Compress uncompressed and Zstd-compressed data|Compresses subblocks that were originally uncompressed or compressed with zstd. |
| Compress all data                             |Compresses all subblocks regardless of current compression method (if possible). |
| Decompress all data                           |Decompresses all possible subblocks.  |
| Dry Run                                       |Finds all CZI files in the input folder but does not create any output CZI files. Like the other operations, it creates a CSV report in the output folder.|

## Progress Update
CZI Shrink scans the input folder for CZI files.
And whenever it finds one, the file is immediately queued for processing.
CZI Shrink will usually process several files in parallel, depending on the state of the queue and the number of CPUs in your computer.
Progress is displayed for all files that are currently being processed.

![Progress Updates](.images/Progress.png)

## Compression Statistics

Compression statistics are updated during the operation.
They provide some insight about what is going on.

![Compression Statistics](.images/CompressionStatistics.png)

### Sharing

Upon completing an operation a new button should appear which will copy a bitmap to your clipboard containing a badge showing off how much the application was able to compress your files.

![Compression Statistics Badge](.images/ShareBadgeExample.png)

## Errors

Files that are unable to be (un)compressed will appear in the bottom dialog with their error message. These entries can also be found in the [log file](#logs).
The error list also allows navigating directly to the files in question.

![Error List](.images/ErrorFileOptions.png)


## Logs

Logs are saved in the output directory.
The filename of this log is constructed as `CziShrink_<TIMESTAMP>_<COMPRESSION_OPTION>`.
The timestamp itself is in the form of `<YEAR><MONTH><DAY>T<HOUR><MINUTES><SECONDS>` while the `COMPRESSION_OPTION` will correspond
to the operation selected in the UI.

Logs are produced during operation, however flushing of buffer to file does not occur as fast
as possible, so you may notice/experience a latency between the tasks that appear on the UI and
the actual entries written.
In some cases you may see no log output until the operation has completed if a reasonably constrained input directory is chosen.

### Format
| InputFile | SizeInput | SizeOutput | SizeRatio | SizeDelta | TimeToProcess | Status | ErrorMessage|
| ----------|-----------|------------|-----------|-----------|---------------|--------|-------------|
| Sample_file.czi | 696480 | 564992  |0.811210659| 131488    | 00:00:00.0672709 |SUCCESS||
| Sample_file2.czi| 1107888032 | 0 | 0 | -1 | |ERROR | Illegal data detected at offset 235352768 -> Invalid SubBlock-magic |
## FAQ
- Is the compression lossy?
  - The tool uses [Zstandard](https://github.com/facebook/zstd) which is a fast and lossless compression algorithm.
- What happens to files that the tool is not able to compress? Are they still copied to the output folder?
  - The file is not copied to the output folder and a log entry detailing the error is produced.


## Known Issues

- Folder enumeration fails on network drives if connection drops.
- Non-X-Y-subblock CZIs will produce an error when attempting to compress.
- Multi-file CZI can't be converted.
  - Ensure file is not currently opened in ZEN.
- Error encountered while compression file rarely causes creation of a corrupted output czi file.

## Potential Future Enhancements

This is a list of possible future enhancements based on feedback.
Any item on this list may be added or removed at any point in time based on prioritization.
There is no guarantee that any items on this list will be added, rather it is just to show what topics are on the radar.
- Localization
- Cache/Remember previously used options (folders, compression, etc) per-user

## Contributing

### Notes for Contributors
* Test changes at least on Linux and Windows.
* Note that the app will change appearance when the system theme is changed (Windows: Settings -> Personalization -> Colors -> Choose your colors: Light/Dark/Custom). Make sure that GUI changes look good both in the Light and in the Dark Theme. See https://docs.avaloniaui.net/docs/next/guides/styles-and-resources/how-to-use-theme-variants

## How to upgrade libczicompressc automatically
1. Create a 'classic' personal access token at https://github.com/settings/tokens, authorize it for the ZEISS organization via the "Configure SSO" button, and store it in a GITHUB_TOKEN environment variable.
2. Open a Powershell terminal and run [./upgrade-libczicompressc.ps1](./upgrade-libczicompressc.ps1). Run `get-help ./upgrade-libczicompressc.ps1` for more info.

## How to upgrade libczicompressc manually
1. Build libczicompressc.so on linux-x64 and libczicompressc.dll on win-x64 in release mode, or (preferred) get them from the [github CI build](https://github.com/ZEISS/czicompress/actions/workflows/czicompress_cmake.yml).
1. Put the binaries into [libczicompressc/runtimes/linux-x64/native](libczicompressc/runtimes/linux-x64/native) and [libczicompressc/runtimes/win-x64/native](libczicompressc/runtimes/win-x64/native)
1. Update the nuspec file [libczicompressc/libczicompressc.nuspec](libczicompressc/libczicompressc.nuspec):
    * `package/metadata/version` must be the 'ProductVersion' of libczicompressc.dll (explorer: Properties/Details)
	* `package/metadata/repository[@commit]` must be the git commit from which the binaries were built
1. [Install nuget if necessary](https://learn.microsoft.com/en-us/nuget/install-nuget-client-tools#cli-tools).
1. Open a shell in [libczicompressc](libczicompressc), and run `path/to/nuget pack libczicompressc.nuspec`
1. Move the resulting nupkg into [packages_local](packages_local) and delete the old package from there.
1. Change the version of libczicompressc in [Directory.Packages.props](Directory.Packages.props)
1. If major or minor version has changed, change the expected version number in [PInvokeFileProcessor](netczicompress/Models/PInvokeFileProcessor.cs).
1. Rebuild netczicompress.sln
1. Run netczicompressTests
1. Undo git changes to the libczicompressc.dll and libczicompressc.so files (no need to commit them, they are in the nupkg).
1. Commit the remaining changes with message "Upgrade libczicompressc to new version: x.y.z"
