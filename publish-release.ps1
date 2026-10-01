#Requires -Version 7.0
[CmdletBinding()]
param(
    # Create a draft release.
    [switch]$Draft,
    # Upload existing files from setup/ without rebuilding.
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$rootDir = $PSScriptRoot
$repository = "J-udgW05/SP-Converter"
$projectPath = Join-Path $rootDir "src\SPConverter.csproj"
$setupDir = Join-Path $rootDir "setup"

# Returns true if gh exits with code 0.
function Test-GhCommand {
    param([Parameter(Mandatory)][string[]]$Arguments)

    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & gh @Arguments *> $null
        return $LASTEXITCODE -eq 0
    }
    finally {
        $ErrorActionPreference = $previous
        $global:LASTEXITCODE = 0
    }
}

if (-not (Get-Command "gh" -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) was not found. Install it from https://cli.github.com/ and run 'gh auth login'."
}

if (-not (Test-GhCommand -Arguments @("auth", "status"))) {
    throw "GitHub CLI is not signed in. Run 'gh auth login' first."
}

[xml]$projectXml = Get-Content -LiteralPath $projectPath
$version = ($projectXml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version.Trim()
$tag = "v$version"

if (Test-GhCommand -Arguments @("release", "view", $tag, "--repo", $repository)) {
    throw "Release $tag already exists. Raise <Version> in src\SPConverter.csproj first."
}

Write-Host "SP Converter release $tag -> $repository"
Write-Host ""

if (-not $SkipBuild) {
    & (Join-Path $rootDir "build-release.ps1")
    Write-Host ""
}

$installer = Join-Path $setupDir "SPConverter_Setup_v$version.exe"
$portable = Join-Path $setupDir "SPConverter_v${version}_Portable.zip"
foreach ($file in @($installer, $portable)) {
    if (-not (Test-Path -LiteralPath $file)) {
        throw "Release file was not found: $file"
    }
}

$notesPath = Join-Path ([System.IO.Path]::GetTempPath()) "SPConverter-release-notes-$version.md"
# UTF-8 without BOM.
$utf8 = New-Object System.Text.UTF8Encoding($false)
$notes = @"
Batch image format converter. Windows 10 or 11, 64-bit; no .NET installation required.

| File | How it installs |
|---|---|
| ``SPConverter_Setup_v$version.exe`` | Installer |
| ``SPConverter_v${version}_Portable.zip`` | Portable build |

The build is not signed: SmartScreen will show "Unknown publisher". Choose "More info" and then "Run anyway".

The build includes Ghostscript (© Artifex Software, AGPL-3.0) for reading PDF files: [THIRD-PARTY-NOTICES.md](https://github.com/$repository/blob/$tag/THIRD-PARTY-NOTICES.md).
"@
[System.IO.File]::WriteAllText($notesPath, $notes, $utf8)

$arguments = @(
    "release", "create", $tag, $installer, $portable,
    "--repo", $repository,
    "--title", "SP Converter $version",
    "--notes-file", $notesPath,
    "--target", "main"
)
if ($Draft) {
    $arguments += "--draft"
}

Write-Host "Uploading release files..."
$ErrorActionPreference = "Continue"
& gh @arguments
$publishExitCode = $LASTEXITCODE
$ErrorActionPreference = "Stop"
if ($publishExitCode -ne 0) {
    throw "Publishing the release failed."
}

Remove-Item -LiteralPath $notesPath -ErrorAction SilentlyContinue
Write-Host ""
if ($Draft) {
    Write-Host "Draft release $tag created. Publish it on GitHub when ready."
}
else {
    Write-Host "Release $tag published."
}
