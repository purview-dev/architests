<#
.SYNOPSIS
	Runs the whole test suite on Linux, in a container.

.DESCRIPTION
	AGENTS.md requires the suite to pass identically on Windows, Linux and macOS, and most of this
	repository reads paths: source roots, assembly roots, globs, the API baseline. Those are
	exactly where Windows and Linux differ - separators, and a case-sensitive filesystem - so a
	suite that has only ever run on Windows has not been shown to meet that requirement.

	The repository's pinned SDK is installed inside the container rather than relying on the image
	tag, because the published preview image predates the pinned release and global.json's
	rollForward would reject it.

	The local NuGet feed is mounted and registered, because global.json pins a Purview.BuildSdk
	version that is published there rather than to nuget.org.
#>
[CmdletBinding()]
param(
	[string]$Image = "mcr.microsoft.com/dotnet/sdk:11.0-preview",
	[string]$LocalFeed = $(if ($env:LOCAL_NUGET_FEED_PATH) { $env:LOCAL_NUGET_FEED_PATH } else { "P:/_sync-projects/.local-nuget" }),
	[string]$Filter = "/*/*/*/*/"
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
	throw "Docker is required to run the Linux test suite."
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$feed = (Resolve-Path $LocalFeed).Path

$script = @"
set -euo pipefail

# Copied out of the read-only mount so the build writes into the container, not the host.
tar --exclude='.git' --exclude='.vs' --exclude='bin' --exclude='obj' \
    --exclude='artifacts' --exclude='TestResults' --exclude='node_modules' \
    -C /repo -cf - . | tar -C /work -xf -
cd /work

# The exact SDK global.json pins. The image tag is only the bootstrap.
SDK_VERSION=`$(grep -oP '"version"\s*:\s*"\K[^"]+' global.json | head -1)
echo "Installing SDK `$SDK_VERSION"
curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
bash /tmp/dotnet-install.sh --version "`$SDK_VERSION" --install-dir /usr/share/dotnet --no-path

# The test projects target net10.0, so the .NET 10 runtime has to be present to launch them. The
# SDK alone is not enough: it builds for the target, it does not run it.
bash /tmp/dotnet-install.sh --channel 10.0 --runtime dotnet --install-dir /usr/share/dotnet --no-path

dotnet --version

dotnet nuget add source /feed --name local-feed --configfile nuget.config
dotnet restore src/ArchitectureTesting.slnx
dotnet build src/ArchitectureTesting.slnx --configuration Release --no-restore
dotnet test --solution src/ArchitectureTesting.slnx --configuration Release --no-build -- --treenode-filter '$Filter'
"@

docker run --rm `
	--mount "type=bind,source=$repositoryRoot,target=/repo,readonly" `
	--mount "type=bind,source=$feed,target=/feed,readonly" `
	--workdir /work `
	--env DOTNET_CLI_TELEMETRY_OPTOUT=1 `
	--env DOTNET_NOLOGO=1 `
	$Image `
	bash -c $script

if ($LASTEXITCODE -ne 0) {
	throw "The Linux test suite failed with exit code $LASTEXITCODE."
}

Write-Host "Linux test suite passed." -ForegroundColor Green
