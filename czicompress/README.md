# czicompress
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![REUSE status](https://api.reuse.software/badge/github.com/ZEISS/czicompress)](https://api.reuse.software/info/github.com/ZEISS/czicompress)
[![CMake](https://github.com/ZEISS/czicompress/actions/workflows/czicompress_cmake.yml/badge.svg?branch=main&event=push)](https://github.com/ZEISS/czicompress/actions/workflows/czicompress_cmake.yml)
[![CodeQL](https://github.com/ZEISS/czicompress/actions/workflows/czicompress_codeql.yml/badge.svg?branch=main&event=push)](https://github.com/ZEISS/czicompress/actions/workflows/czicompress_codeql.yml)
[![MegaLinter](https://github.com/ZEISS/czicompress/actions/workflows/mega-linter.yml/badge.svg?branch=main&event=push)](https://github.com/ZEISS/czicompress/actions/workflows/mega-linter.yml)

Reduce the size of existing CZI files by converting them to zstd-compressed CZI files.

Copies the content of a CZI-file into another CZI-file changing the compression of the image data.
With the 'compress' command, uncompressed image data is converted to Zstd-compressed image data.
With the 'decompress' command, compressed image data is converted to uncompressed data.

The tool is based on [libczi](https://github.com/ZEISS/libczi.git).

## Table of contents

- [Download](#download)
- [Usage](#usage)
    - [Examples](#examples)
        - [Single file](#single-file)
        - [Multiple files (bash shell)](#multiple-files-bash-shell)
        - [Multiple files (Powershell)](#multiple-files-powershell)
- [Build and Test](#build-and-test)
    - [Pre-requisites](#pre-requisites)
    - [Build in Visual Studio](#build-in-visual-studio)
    - [Build in command line](#build-in-command-line)
        - [Quick build](#quick-build)
        - [Build with preferred compiler](#build-with-preferred-compiler)
    - [Tests](#tests)
- [Known issues](#known-issues)
- [Guidelines](#guidelines)
- [Versioning](#versioning)
- [Licensing](#licensing)
- [Credits to Third Party Components](#credits-to-third-party-components)
- [Disclaimer](#disclaimer)

## Download
We have not yet published any Releases. Meanwhile, you can download binaries from the artifacts of the latest [Build workflow run](https://github.com/zeissmicroscopy/czicompress/actions/workflows/cmake.yml?query=branch%3Amain).
Click on the topmost successful run and download the binaries for your platform:
* czicompress-windows-64-release-msvc-package-off for Windows
* czicompress-ubuntu-release-llvm-package-off for Linux

Clicking on these artifacts will download a ZIP file with the executable. The executable is a stand-alone binary. You can put it anywhere you like. 

## Usage

Start the executable from the command line, providing the required command line arguments.

```
Usage: czicompress.exe [OPTIONS]

Options:
  -h,--help         Print this help message and exit

  -c,--command COMMAND
                    Specifies the mode of operation: 'compress' to convert to a
                    zstd-compressed CZI, 'decompress' to convert to a CZI
                    containing only uncompressed data.

  -i,--input SOURCE_FILE
                    The source CZI-file to be processed.

  -o,--output DESTINATION_FILE
                    The destination CZI-file to be written.

  -s,--strategy STRATEGY
                    Choose which subblocks of the source file are compressed.
                    STRATEGY can be one of 'all', 'uncompressed',
                    'uncompressed_and_zstd'. The default is 'uncompressed'.

  -t,--compression_options COMPRESSION_OPTIONS
                    Specify compression parameters. The default is
                    'zstd1:ExplicitLevel=1;PreProcess=HiLoByteUnpack'.

  -w,--overwrite    If the output file exists, try to overwrite it.

  --ignore_duplicate_subblocks BOOLEAN
                    If this option is enabled, the operation will ignore if
                    duplicate subblocks are encountered in the source document.
                    Otherwise, an error will be reported. The default is 'on'.


Copies the content of a CZI-file into another CZI-file changing the compression
of the image data.
With the 'compress' command, uncompressed image data is converted to
Zstd-compressed image data. This can reduce the file size substantially. With
the 'decompress' command, compressed image data is converted to uncompressed
data.
For the 'compress' command, a compression strategy can be specified with the
'--strategy' option. It controls which subblocks of the source file will be
compressed. The source document may already contain compressed data (possibly
with a lossy compression scheme). In this case it is undesirable to compress the
data with lossless zstd, as that will almost certainly increase the file size.
Therefore, the "uncompressed" strategy compresses only uncompressed subblocks.
The "uncompressed_and_zstd" strategy compresses the subblocks that are
uncompressed OR compressed with Zstd, and the "all" strategy compresses all
subblocks, regardless of their current compression status. Some compression
schemes that can occur in a CZI-file cannot be decompressed by this tool. Data
compressed with such a scheme will be copied verbatim to the destination file,
regardless of the command and strategy chosen.
```

### Examples

#### Single file
~~~
czicompress -c compress -i MyImage.czi -o MyImage.zstd.czi
~~~

#### Multiple files (bash shell)
* Put czicompress on the PATH
~~~cs
export PATH="${PATH}:/dir/of/czicompress"
~~~
* Put the following lines into a file called czicompress.sh:
~~~sh
#!/bin/bash
f="$1"
output="${f%%czi}zstd.czi"
echo -n "${f} -> ${output}: "
if [[ -f "$output" ]]
then 
    echo "Output file already exists."
    rm -i "$output"
fi

if [[ -f "$output" ]]
then
    exit 1
fi
    
czicompress --command compress -i "$f" -o "$output"
status=$? 
if [[ $status -eq 0 ]]
then 
    echo OK
fi

exit $status
~~~
* Then run 
~~~sh
find -type f -name '*.czi' -not -iname '*.zstd.czi' -exec bash czicompress.sh '{}' \;
~~~

#### Multiple files (Powershell)
* Put czicompress on the PATH
~~~powershell
$env:Path += ";/dir/of/czicompress"
~~~
* Put the following lines into a file called czicompress.ps1:
~~~powershell
param(
  [Parameter(Mandatory=$true)]
  [string]$directory,
  [Parameter(Mandatory=$false)]
  [string]$command = "compress",
  [Parameter(Mandatory=$false)]
  [string]$fileExtension = "czi",
  [Parameter(Mandatory=$false)]
  [string]$outputSuffix = ".zstd",
  [Parameter(Mandatory=$false)]
  [switch]$recursive = $false
)

$cziCompressExe = "czicompress.exe"

$filterString = "*.$($fileExtension)"

if ($recursive) {
  $files = Get-ChildItem -Path $directory -Filter $filterString -Recurse
} else {
  $files = Get-ChildItem -Path $directory -Filter $filterString
}

$files = $files | Where-Object {
  $_.Name -notlike "*$outputSuffix$filterString"
}

$files | ForEach-Object {
  $outFileName = "$($_.DirectoryName)\$($_.BaseName)$outputSuffix$($_.Extension)"
  Write-Host "$($_) -> $($outFileName)"
  if ((Test-Path -Path $outFileName)) {
	  Write-Host "Output file already exists... Removing"
	  Remove-Item $outFileName
  }
  if ((Test-Path -Path $outFileName)) {
	  Write-Host "Unable to remove file"
	  # This will act as a continue because this code is executed as a script block and not like a classic for-each loop
	  return
  }

  # The backticks here are to escape the double quotes which exist in case we have spaces in file path.
  $execArguments = "& $($cziCompressExe) -i `"$_`" -o `"$($outFileName)`" --command $($command)"
  iex $execArguments
}

exit 0
~~~
* Then run
~~~powershell
./czicompress.ps1 -Directory .
~~~

## Build and Test

Build either with Visual Studio or with CMake in command line.

### Pre-requisites

The `CZICompress` source codes are implemented using C++17 standard and can be compiled on Windows or Linux and with multiple compilers. You can compile codes by using IDE like Microsoft Visual Studio (MSVS), Visual Studio Code (VSCode) or any other IDE that works with `CMake`. Make sure that on your machine you have one of these tools:

|            | Windows             | Linux         |
| ---------- | ------------------- | ------------- |
| Build tool | CMake               | CMake         |
| Compilers  | MSVC                | GNU, LLVM     |
| IDE        | MSVS, VSCode, CLion | VSCode, CLion |


### Build in Visual Studio

To build `czicompress` in *Microsoft Visual Studio* (or in *Visual Studio Code*) follow these steps:
1. Open `czicompress` folder in Visual Studio.
2. In the *Solution Explorer* select `CMakeLists.txt` file and make right click.
3. In the popped up menu select `Configure czicompress` (`Configure All Projects` in VSCode).
4. When configuration is finished, right click on `CMakeLists.txt` and the popped up menu select `Build` (`Build All Projects` in the VSCode).

This configures and builds software using default compilers.

### Build in command line

#### Quick build

On Windows or Linux platform, open the command line window in the cloned `czicompress` directory and type this to configure and build the sources:

```bash
cmake -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build -j 10
```

This configures `Release` build using system default compiler. After the build, for the Windows platform the `czicompress.exe` executable  is located in the `.\build\app\Release\` subdirectory of `czicompress` project directory and for the Linux it is located in the `./build/app/` subdirectory of `czicompress` project directory.

#### Build with preferred compiler

If you want to compile using a specific preferred compiler, either set environment variables `CXX` for the C++ compiler and `CC` for the C compiler, or specify compilers as `cmake` options.

**Example 1:** if you want to compile under Linux or WSL ([Windows Subsystem for Linux](https://learn.microsoft.com/en-us/windows/wsl/about) with LLVM compilers, before configuring build with `cmake` set environment variables.
```bash
export CC=/usr/bin/clang && export CXX=/usr/bin/clang++
cmake -B ./build -DCMAKE_BUILD_TYPE=Release
```

**Example 2:** set the preferred C/C++ compilers as `cmake` options
```bash
cmake -B ./build -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER=clang -DCMAKE_CXX_COMPILER=clang++
```

When the configuration is finished, make a build and via parameter `-j` specify the number of parallel jobs you want to run:
```bash
cmake --build ./build --config Release -j 10
```

After the build, the `czicompress` executable will be in `./build/app/czicompress`.

### Tests

After the build, run the tests by executing `./build/tests/czicompress_tests`.

## Known issues

When compiling in Visual Studio for the first time, you may need to double compile. The Visual Studio is using "Ninja" for building and for some reason it is reporting a linker error `LINK: Fatal error LNK1168: cannot open app\czicompress.exe for writing` despite the fact that the `czicompress.exe` is created.

## Guidelines
[Code of Conduct](./CODE_OF_CONDUCT.md)  
[Contributing](./CONTRIBUTING.md)

## Versioning
For czicompress the [semantic versioning scheme 2.0](https://semver.org/) is to be applied. The version number is defined in the top-level [CMakeLists.txt](./CMakeLists.txt). This is the only place where the version number is defined. When making changes, this version number needs to be updated accordingly. For determining whether a change is a breaking one, the source code level is decisive; i.e. not binary compatibility (of the resulting static or dynamic library) is to be considered, but the case of re-compiling with the changed sources. A borderline case are maybe changes in the CMake-files - here the best judgement is to be applied whether a change in the compilation result will occur.

## Licensing
Carl Zeiss Microscopy GmbH provides czicompress under the [MIT License](./LICENSE).

## Credits to Third Party Components
As part of our CI/CD, the following 3rd party components are re-distributed:  
- [libCZI + 3rd Party Source Code Components](https://github.com/ZEISS/libczi/tree/main#credits-to-third-party-components)
- [libCZI Linked Dependencies + czicompress Linked Dependencies](./THIRD_PARTY_LICENSES_ARTIFACT_DISTRIBUTION.txt)

## Disclaimer
ZEISS, ZEISS.com are registered trademarks of Carl Zeiss AG.
