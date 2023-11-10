<#
.SYNOPSIS

Upgrades libczicompressc.nupkg in CziShrink with the latest cmake build of the main branch of czicompress.

.DESCRIPTION

This script should be run in the czishrink directory of a clone of https://github.com/ZEISS/czicompress.
Make sure that you do not have any uncommited changes before you run the script. Otherwise the script will stash such changes.

You must have git and dotnet on your path.

The script needs to have access to the https://github.com/ZEISS/czicompress github repo (or your fork of that repo),
you need to
	* create a 'classic' personal access token at https://github.com/settings/tokens or with github cli,
	* and authorize it for the ZEISS organization via the "Configure SSO" button,
	* and store it in a GITHUB_TOKEN environment variable or pass it as the -GithubToken parameter to this script.

If you specify a -DownloadFolder the script will reuse data downloaded to that folder in a previous run. This provides rudimentary resume-on-error functionality.

.EXAMPLE

PS> ./upgrade-libczicompressc.ps1 -DownloadFolder "$env:TEMP/libczicompress/cache" -NoPullRequest

.PARAMETER DownloadFolder

The folder where downloads are stored and cached. Defaults to "$env:Temp/libczicompressc/$(Get-Date -Format FileDate)".
Downloads in that folder will be reused unless you use -NoCache.
The folder is created if necessary.
The folder will not be deleted or emptied at the end of the script.

.PARAMETER NugetExePath

The path to Nuget.exe. If this is not specified and nuget.exe is found on the $env:PATH we use that nuget.exe.
Otherwise defaults to DownloadFolder/nuget.exe. If nuget.exe is not found it is downloaded and stored at this path.

.PARAMETER GithubToken

The github personal access token to use. Can be created at https://github.com/settings/tokens or with github cli (gh auth token).
Defaults to the value of $env:GITHUB_TOKEN.

.PARAMETER NoPR

This switch prevents a pull request from being created.

.PARAMETER NoCache

This switch prevents previous downloads in the DownloadFolder from being reused.

.PARAMETER OverwriteExistingBranch

Overwrites an existing upgrade branch. This can be useful if a previous run of the script has failed. Applies to local and remote.

#>

param(
  [Parameter(Mandatory=$false)]
  [AllowNull()]
  [string]$DownloadFolder = $null,

  [Parameter(Mandatory=$false)]
  [string]$NugetExePath = $null,

  [Parameter(Mandatory=$false)]
  [string]$GithubToken = $env:GITHUB_TOKEN,

  [Parameter(Mandatory=$false)]
  [switch]$NoPR,

  [Parameter(Mandatory=$false)]
  [switch]$NoCache,

  [Parameter(Mandatory=$false)]
  [switch]$OverwriteExistingBranch
)

$CreatePullRequest = !$NoPR
$UseCache = !$NoCache

Import-Module -Name Microsoft.PowerShell.Utility
Import-Module -Name Microsoft.PowerShell.Archive

$ErrorActionPreference = "Stop"

# The name of the CziCompress repo
$CziCompress = "ZEISS/czicompress"
##########################################################
# PART 1: Download latest CmakeBuild artifacts from GitHub
##########################################################

if ($null -eq $GithubToken) {
	throw "You must set the GITHUB_TOKEN environment variable to a personal access token of yours: https://github.com/settings/tokens. See also: https://docs.github.com/en/enterprise-cloud@latest/authentication/authenticating-with-saml-single-sign-on/authorizing-a-personal-access-token-for-use-with-saml-single-sign-on"
}

$GithubApiHeaders = @{
	'Accept' = 'application/vnd.github+json'
	'X-GitHub-Api-Version' = '2022-11-28'
	'Authorization' = "Bearer $GithubToken"
}

if (!$DownloadFolder) {
	$DownloadFolder = "$env:Temp/libczicompressc/$(Get-Date -Format FileDate)"
}

Write-Output "INFO: Using DownloadFolder $DownloadFolder"
if (!(Test-Path $DownloadFolder)) {
	mkdir $DownloadFolder
}

# We'll use the following properties
# artifacts_url -> download dll/so
# head_sha -> nuspec git commit
$LatestRunJson = "$DownloadFolder/LatestRunOfCMakeBuild.json"

if ($UseCache -and (Test-Path -Path $LatestRunJson)) {
	Write-Output "INFO: Using previously downloaded build info from $LatestRunJson."
	$LatestRunOfCMakeBuild = Get-Content $LatestRunJson	| ConvertFrom-Json
} else {
	# Get latest run
	Write-Output "INFO: Getting build info from github.com/$CziCompress."
	$ActionRunsUri = "https://api.github.com/repos/$CziCompress/actions/runs?branch=main&status=success&per_page=10"
	$ServerResponse = Invoke-WebRequest -Uri $ActionRunsUri -Headers $GithubApiHeaders | ConvertFrom-Json
	$RunsOfCmakeBuild = $ServerResponse.workflow_runs | Where-Object Name -eq 'CMake Build (czicompress)'

	$LatestRunOfCMakeBuild = $RunsOfCmakeBuild | Sort-Object -Property 'run_number' | Select-Object -Last 1
	$LatestRunOfCMakeBuild | ConvertTo-Json | Set-Content -Path $LatestRunJson
}

$LinuxArtifactZip = "$DownloadFolder/linux.zip"
$WindowsArtifactZip = "$DownloadFolder/win.zip"
$DownloadLinux = $NoCache -or !(Test-Path $LinuxArtifactZip)
$DownloadWindows = $NoCache -or !(Test-Path $WindowsArtifactZip)

if ($DownloadWindows -or $DownloadLinux)
{
	# Download the artifacts
	Write-Output "INFO: Downloading artifacts from $($LatestRunOfCMakeBuild.artifacts_url)"
	$ServerResponse = Invoke-WebRequest -Uri $LatestRunOfCMakeBuild.artifacts_url  -Headers $GithubApiHeaders | ConvertFrom-Json

	$LinuxArtifact = $ServerResponse.artifacts | Where-Object name -eq "czicompress-ubuntu-release-package-on"
	$WindowsArtifact = $ServerResponse.artifacts | Where-Object name -eq "czicompress-windows-64-release-msvc-package-on"

	if ($ServerResponse.total_count -eq 0) {
		throw "No artifacts found."
	}

	try {
		if ($DownloadLinux)
		{
			Invoke-WebRequest -Uri $LinuxArtifact.archive_download_url -Headers $GithubApiHeaders -OutFile $LinuxArtifactZip
		}

		if ($DownloadWindows)
		{
			Invoke-WebRequest -Uri $WindowsArtifact.archive_download_url -Headers $GithubApiHeaders -OutFile $WindowsArtifactZip
		}
	} catch {
		Write-Output "ERROR: unable to download artifacts."
		throw
	}
} else {
	Write-Output "INFO: Using previously download artifact ZIP files in $DownloadFolder"
}

# Extract libczicompressc binaries
Expand-Archive -Path $LinuxArtifactZip -Force -DestinationPath "$DownloadFolder"
Expand-Archive -Path $WindowsArtifactZip -Force -DestinationPath "$DownloadFolder"

#############################################
# Part 2: Build and use the new nuget package
#############################################

# Get the new library version
$NewLibVersion = (Get-Item "$DownloadFolder/libczicompressc.dll").VersionInfo.ProductVersion.Replace(",", ".")
$NewLibVersion_ = $NewLibVersion.Replace(".", "_")
$NewFileVersion = (Get-Item "$DownloadFolder/libczicompressc.dll").VersionInfo.FileVersionRaw

$BranchName = "feature/upgrade-libczicompressc-$NewLibVersion_"
Write-Output "INFO: The new library version is $NewLibVersion => Create branch $BranchName and switch to it."

if (![string]::IsNullOrEmpty($PSScriptRoot)) {
	Set-Location "$PSScriptRoot/libczicompressc"
} else {
	$current_dir_name = Get-Location | Split-Path -Leaf
	if ("czishrink" -eq $current_dir_name)
	{
		Set-Location "libczicompressc"
	}  elseif ("libczicompress" -ne $current_dir_name) {
		throw "Please CD to the /czishrink directory of a czicompress clone."
	}
}

# Stash any local changes
$GitStatus = git status --porcelain
if($LASTEXITCODE -ne 0) {
	throw "git status failed"
}

if (![string]::IsNullOrEmpty($GitStatus)) {
	Write-Output "INFO: Stashing local changes"
	git stash --include-untracked
	if($LASTEXITCODE -ne 0) {
		throw "git stash failed"
	}
}

# Switch to main and pull
git switch main
if($LASTEXITCODE -ne 0) {
	throw "git switch failed"
}

git pull
if($LASTEXITCODE -ne 0) {
	throw "git pull failed"
}

# Create a new branch
$gitSwitchCreateFlag = "-c"
if ($OverwriteExistingBranch) {
	$gitSwitchCreateFlag = "-C"
}

git switch $gitSwitchCreateFlag $BranchName
if($LASTEXITCODE -ne 0) {
	throw "Failed to create branch $BranchName. Please delete the branch if it exists."
}

# Put the binaries into the nuget folder structure
Write-Output "INFO: Creating the new nuget package..."
$libname="libczicompressc"
$LinuxRuntime="runtimes\linux-x64\native\$libname.so"
$WindowsRuntime="runtimes\win-x64\native\$libname.dll"
Copy-Item -Path "$DownloadFolder/$libname.so" -Destination $LinuxRuntime
Copy-Item -Path "$DownloadFolder/$libname.dll" -Destination $WindowsRuntime

# Modify the nuspec
$nuspec=Get-Item "libczicompressc.nuspec"
$xml = [xml](Get-Content -Path $nuspec.FullName)
$xml.package.metadata.version = $NewLibVersion
$xml.package.metadata.repository.commit = $LatestRunOfCMakeBuild.head_sha
$xml.Save($nuspec.FullName)

# Download nuget.exe if necessary and run nuget pack
if (!$NugetExePath) {
	try {
		$NugetExePath = (Get-Command "nuget.exe").Source
	} catch {
		$NugetExePath = "$DownloadFolder/nuget.exe"
	}
}

if (!(Test-Path -Path $NugetExePath)) {
	Write-Output "INFO: Downloading nuget.exe to $NugetExePath"
	Invoke-WebRequest -Uri https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile $NugetExePath
} else {
	Write-Output "INFO: Using nuget.exe at $NugetExePath"
}

& $NugetExePath pack "libczicompressc.nuspec"
if($LASTEXITCODE -ne 0) {
	throw "nuget failed"
}

# Revert changes to so/dll
git checkout -- $LinuxRuntime
if($LASTEXITCODE -ne 0) {
	throw "git checkout failed"
}

git checkout -- $WindowsRuntime
if($LASTEXITCODE -ne 0) {
	throw "git checkout failed"
}

# Exchange the nupkg and update version in Directory.Packages.props
Remove-Item ../packages_local/*.nupkg
Move-Item -Path "libczicompressc.$NewLibVersion.nupkg" -Destination ../packages_local

Write-Output "INFO: Updating Directory.Packages.props"
Set-Location ..
$file = Get-Item "Directory.Packages.props"
$xml = [xml](Get-Content -Path $file.FullName)
$versionElement = $xml.SelectSingleNode('//PackageVersion[@Include="libczicompressc"]')
$versionElement.Version = $NewLibVersion
$xml.Save($file.FullName)

################################################
# Part 3: Update, build, and test netczicompress
################################################

# Update PInvokeFileProcessor.cs
Write-Output "INFO: Updating PInvokeFileProcessor.cs"
$SourceCode = Get-Content -Path netczicompress/Models/PInvokeFileProcessor.cs
$NewVersionTuple="($($NewFileVersion.Major), $($NewFileVersion.Minor))"
$AlteredSourceCode = $SourceCode -replace '^( *\(int Major, int Minor\) expected = ).*?;$',('$1' + $NewVersionTuple + ";")
Set-Content -Path netczicompress/Models/PInvokeFileProcessor.cs -Value $AlteredSourceCode

# Increment VersionPrefix
Write-Output "INFO: Incrementing VersionPrefix in Directory.Build.props"
$file = Get-Item "Directory.Build.props"
$xml = [xml](Get-Content -Path $file.FullName)
$versionElement = $xml.SelectSingleNode('//VersionPrefix')
$VersionTokens = $versionElement.'#text'.split(".")
$versionElement.'#comment' = $BranchName
$VersionTokens[1] = [string]([int]($VersionTokens.split(".")[1]) + 1)
$versionElement.'#text' = "$($VersionTokens[0]).$($VersionTokens[1]).$($VersionTokens[2])"
$xml.Save($file.FullName)

# Git commit everything
Write-Output "INFO: Committing all changes"
$CommitMessage = "Upgrade $libname to $NewLibVersion"
git add .
if($LASTEXITCODE -ne 0) {
	throw "git add failed"
}
git commit -m $CommitMessage
if($LASTEXITCODE -ne 0) {
	throw "git commit failed"
}

# Run tests
Write-Output "INFO: Run tests"
dotnet test
if($LASTEXITCODE -ne 0) {
	throw "dotnet test failed"
}

# Git Push
Write-Output "INFO: Push changes"

$gitForceFlag = ""
if ($OverwriteExistingBranch) {
	$gitForceFlag = "--force"
}

git push $gitForceFlag --set-upstream origin $BranchName
if($LASTEXITCODE -ne 0) {
	throw "git push failed"
}

# Create a pull request
if ($CreatePullRequest) {
	$remote = git config --get remote.origin.url
	if($LASTEXITCODE -ne 0) {
		throw "git config failed"
	}

	$CziShrinkRepo = $remote.Replace("https://github.com/", "") -replace "\.git$", ""

	Write-Output "INFO: Create a Pull Request"
	$postParams = @{
		'title'=$CommitMessage
		'body'="Automatic upgrade of $libname"
		'head'=$BranchName
		'base'='main'
	} | ConvertTo-Json

	$ServerResponse = Invoke-WebRequest  -Headers $GithubApiHeaders -Uri https://api.github.com/repos/$CziShrinkRepo/pulls -Method POST -Body $postParams
	$PullRequestUrl = ($ServerResponse | ConvertFrom-Json).html_url
	Write-Output "Opened a PR at $PullRequestUrl"
	Start-Process $PullRequestUrl
}
