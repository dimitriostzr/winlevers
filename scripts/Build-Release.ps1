<#
.SYNOPSIS
    Builds the release files: a portable zip and an installer per architecture.

.DESCRIPTION
    Publishes WinLevers.App self-contained (the .NET runtime and the Windows App
    SDK beside the exe, so it runs on a machine with neither installed), zips
    that folder as the portable build, and compiles installer/WinLevers.iss
    around the same folder. Everything lands in artifacts/release.

    The release workflow runs this on GitHub Actions; it runs the same way on a
    Windows machine with the .NET 10 SDK and Inno Setup 6 installed.

.EXAMPLE
    ./scripts/Build-Release.ps1
    ./scripts/Build-Release.ps1 -Arch arm64 -SkipInstaller
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')]
    [string[]] $Arch = @('x64', 'arm64'),

    # Defaults to <Version> in Directory.Build.props, so the files can never be
    # named for a version the About page does not show.
    [string] $Version,

    [switch] $SkipInstaller
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts\release'

if (-not $Version) {
    $Version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
}
if (-not $Version) { throw 'No <Version> found in Directory.Build.props.' }

$iscc = $null
if (-not $SkipInstaller) {
    $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
    if (-not $iscc) {
        $iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
                  "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
                  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") |
            Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (-not $iscc) { throw 'Inno Setup 6 (ISCC.exe) not found. Install it, or pass -SkipInstaller for the portable zip only.' }
}

New-Item -ItemType Directory -Force $out | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($a in $Arch) {
    $platform = @{ x64 = 'x64'; arm64 = 'ARM64' }[$a]
    $rid = "win-$a"

    # Published into a folder named WinLevers so the zip unpacks to one.
    $publish = Join-Path $root "artifacts\publish\$rid\WinLevers"
    if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }

    Write-Host "== Publishing $rid"
    dotnet publish (Join-Path $root 'src\WinLevers.App') -c Release -r $rid "-p:Platform=$platform" `
        -p:SelfContained=true -p:WindowsAppSDKSelfContained=true -o $publish
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid." }

    $zip = Join-Path $out "WinLevers-$Version-$rid-portable.zip"
    if (Test-Path $zip) { Remove-Item -Force $zip }
    Write-Host "== Zipping $zip"
    [System.IO.Compression.ZipFile]::CreateFromDirectory($publish, $zip,
        [System.IO.Compression.CompressionLevel]::Optimal, $true)

    if ($iscc) {
        Write-Host "== Building installer for $rid"
        & $iscc /Q "/DAppVersion=$Version" "/DArch=$a" "/DSourceDir=$publish" "/DOutputDir=$out" `
            (Join-Path $root 'installer\WinLevers.iss')
        if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed for $rid." }
    }
}

Get-ChildItem $out | Format-Table Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } -AutoSize
