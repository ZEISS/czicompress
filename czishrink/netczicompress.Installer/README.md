# CZI Shrink Installer

Welcome to the Project Installer! This installer is designed to simplify the setup process for our application. Follow the instructions below to get started.

## Table of Contents

1. [Configuration](#configuration)
1. [Consuming CZI Shrink](#consuming-czi-shrink)
   1. [External Artifacts](#external-artifacts)
   1. [Custom Publish Task](#custom-publish-task)
   1. [Project Reference](#project-reference)
1. [Signing](#signing)

## Configuration

There are several settings that can be changed by setting environment variables.

| Variable           | Description                                                                                                   | Example                                   |
|--------------------|---------------------------------------------------------------------------------------------------------------|-------------------------------------------|
| CustomVersion      | Set the ProductVersion for the installer directly                                                             | 1.1.39384                                 |
| ArtifactsDirectory | Location of CZI Shrink artifacts. See [requirements](#external-artifacts).                                                    | C:/Build/CziShrink/output                 |
| DoNotPublish       | If set to true will not use custom publish task, rather will rely on project reference to consume CZI Shrink. | true                                      |
| Sign               | If defined will set `SignOutput` true which will enable the signing task in wix toolset.                              | 1, true, anything                      |
| SIGNTOOL_PARAMS    | Parameters to pass to signtool.exe to be executed during sign task                                            | sign /v /f $(TestCertificate) /p password |

## Consuming CZI Shrink

There are several ways to package CZI Shrink into an installer.
The only requirement is that the `x64` Platform Target is used to build it. 

### External Artifacts

The simplest way is to provide pre-build artifacts (either signed or not).
This can be done by setting the environment variable `ArtifactsDirectory` to the location of the files.
It is recommended to only set this locally in the context of your build command `msbuild.exe` or `dotnet build`.
There are a few requirements to what is located in this directory:
 - A file called `CziShrink.exe` must exist
 - A file called `README.pdf` must exist

__It is important that this environment variable is present during restore and build.__
Otherwise, the custom publish task may run for the referenced `netczicompress.Desktop` project.

### Custom Publish Task

If `ArtifactsDirectory` is not defined and `DoNotPublish` is not set then a custom task will run and publish `netczicompress.Desktop` with the following options:
 - `self-contained`
 - `PublishSingleFile` : `true`
 - `PublishReadyToRun`: `true`

This will be created in a new folder `publish` located in the `.wixproj` directory.

### Project Reference

If `DoNotPublish` is set to `true` then `netczicompress.Desktop` will be consumed as a project reference which will create a self-contained deployment.
This can result in many more files and larger installer, but can be useful if you have issues with publishing to a single file or ReadyToRun. 

## Signing

This is done via the wix toolset signing task (see [documentation](https://wixtoolset.org/docs/tools/signing/)). If the `SIGN` environment variable is set this will be enabled.
You will need `signtool.exe` on your path as well as some certificats that you wish to use.
Set `SIGNTOOL_PARAMS` to your signtool arguments.
Please refer to [configuration](#configuration) for examples.