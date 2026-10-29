<#
.SYNOPSIS
    Builds the release files, and with -Publish puts them on GitHub as the one release.

.DESCRIPTION
    Publishes WinLevers.App self-contained (the .NET runtime and the Windows App
    SDK beside the exe, so it runs on a machine with neither installed), zips
    that folder as the portable build, compiles installer/WinLevers.iss around
    the same folder, and writes SHA256SUMS.txt. Everything lands in
    artifacts/release, which is emptied first.

    -Publish then replaces the GitHub release with these files. Only the latest
    build is ever offered: every older release, and the tag behind it, is
    deleted first. It refuses to run unless the working tree is clean and HEAD
    is what origin/main points at, so a release always matches pushed code.

    Needs the .NET 10 SDK and Inno Setup 6; -Publish also needs the GitHub CLI,
    logged in (gh auth login).

.EXAMPLE
    ./scripts/Build-Release.ps1 -Publish
    ./scripts/Build-Release.ps1 -Arch x64 -SkipInstaller
#>
[CmdletBinding()]
param(
    [ValidateSet('x64', 'arm64')]
    [string[]] $Arch = @('x64', 'arm64'),

    # Defaults to <Version> in Directory.Build.props, so the release can never be
    # named for a version the About page does not show.
    [string] $Version,

    [switch] $SkipInstaller,

    [switch] $Publish
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts\release'

if (-not $Version) {
    $Version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version |
        Where-Object { $_ } | Select-Object -First 1
}
if (-not $Version) { throw 'No <Version> found in Directory.Build.props.' }

if ($Publish) {
    if ($SkipInstaller -or $Arch.Count -lt 2) { throw '-Publish needs both architectures and the installers.' }
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI (gh) not found.' }
    gh auth status | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'gh is not logged in. Run: gh auth login' }

    Push-Location $root
    try {
        if (git status --porcelain) { throw 'The working tree has uncommitted changes. Commit and push first.' }
        git fetch -q origin
        $sha = git rev-parse HEAD
        if ($sha -ne (git rev-parse origin/main)) { throw 'HEAD is not origin/main. Push first, so the release matches the code on GitHub.' }
    }
    finally { Pop-Location }
}

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

if (Test-Path $out) { Remove-Item -Recurse -Force $out }
New-Item -ItemType Directory -Force $out | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($a in $Arch) {
    $platform = @{ x64 = 'x64'; arm64 = 'ARM64' }[$a]
    $rid = "win-$a"

    # Published into a folder named WinLevers so the zip unpacks to one.
    $publishDir = Join-Path $root "artifacts\publish\$rid\WinLevers"
    if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }

    Write-Host "== Publishing $rid"
    dotnet publish (Join-Path $root 'src\WinLevers.App') -c Release -r $rid "-p:Platform=$platform" `
        -p:SelfContained=true -p:WindowsAppSDKSelfContained=true -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $rid." }

    # No version in the name, so releases/latest/download/<name> stays a stable link.
    $zip = Join-Path $out "WinLevers-$rid-portable.zip"
    Write-Host "== Zipping $zip"
    [System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zip,
        [System.IO.Compression.CompressionLevel]::Optimal, $true)

    if ($iscc) {
        Write-Host "== Building installer for $rid"
        & $iscc /Q "/DAppVersion=$Version" "/DArch=$a" "/DSourceDir=$publishDir" "/DOutputDir=$out" `
            (Join-Path $root 'installer\WinLevers.iss')
        if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed for $rid." }
    }
}

# The same format as sha256sum, so either tool can check it.
$files = Get-ChildItem $out -File | Sort-Object Name
$sums = ($files | ForEach-Object { '{0}  {1}' -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name }) -join "`n"
[IO.File]::WriteAllText((Join-Path $out 'SHA256SUMS.txt'), "$sums`n")

Get-ChildItem $out | Format-Table Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } } -AutoSize

if ($Publish) {
    $tag = "v$Version"
    Push-Location $root
    try {
        foreach ($t in (gh release list --limit 100 --json tagName -q '.[].tagName')) {
            if ($t) {
                Write-Host "== Deleting release $t"
                gh release delete $t --yes --cleanup-tag
                if ($LASTEXITCODE -ne 0) { throw "Could not delete release $t." }
            }
        }
        # A tag left without a release would pin the new one to an old commit.
        if ((gh api "repos/{owner}/{repo}/git/matching-refs/tags/$tag" -q '.[].ref') -contains "refs/tags/$tag") {
            gh api -X DELETE "repos/{owner}/{repo}/git/refs/tags/$tag" | Out-Null
        }

        Write-Host "== Creating release $tag"
        gh release create $tag (Get-ChildItem $out -File).FullName --target $sha --latest `
            --title "WinLevers $Version" --notes-file (Join-Path $PSScriptRoot 'release-notes.md')
        if ($LASTEXITCODE -ne 0) { throw 'gh release create failed.' }
    }
    finally { Pop-Location }
}