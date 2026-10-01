#Requires -Version 7.0
[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$rootDir = $PSScriptRoot
$projectPath = Join-Path $rootDir "src\SPConverter.csproj"
$testsPath = Join-Path $rootDir "tests\SPConverter.Tests\SPConverter.Tests.csproj"
$installerScriptPath = Join-Path $rootDir "SPConverter.iss"
$publishDir = Join-Path $rootDir "publish\portable"
$setupDir = Join-Path $rootDir "setup"
$toolDir = Join-Path $rootDir "tools\dotnet-tools"
$runtime = "win-x64"

function Resolve-DotNet {
    $dotnetCommand = Get-Command "dotnet" -ErrorAction SilentlyContinue
    if ($dotnetCommand) {
        return $dotnetCommand.Source
    }

    $defaultPath = "C:\Program Files\dotnet\dotnet.exe"
    if (Test-Path -LiteralPath $defaultPath) {
        return $defaultPath
    }

    throw "dotnet.exe was not found. Install .NET SDK or add dotnet.exe to PATH."
}

function Resolve-InnoCompiler {
    param([Parameter(Mandatory)][string]$DotNetPath)

    $candidates = @(
        (Join-Path $toolDir ".store\dotnet-innosetup\6.2.1\dotnet-innosetup\6.2.1\tools\is\ISCC.exe"),
        "C:\Users\Hser\.nuget\packages\tools.innosetup\6.4.2\tools\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    $isccCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($isccCommand) {
        return $isccCommand.Source
    }

    Write-Host "Inno Setup compiler was not found. Installing local dotnet-innosetup tool..."
    New-Item -ItemType Directory -Path $toolDir -Force | Out-Null

    # Keep tool output out of the function result.
    & $DotNetPath tool install dotnet-innosetup --tool-path $toolDir --version 6.2.1 | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Could not install dotnet-innosetup tool."
    }

    $localCompiler = Join-Path $toolDir ".store\dotnet-innosetup\6.2.1\dotnet-innosetup\6.2.1\tools\is\ISCC.exe"
    if (Test-Path -LiteralPath $localCompiler) {
        return $localCompiler
    }

    throw "ISCC.exe was not found after installing dotnet-innosetup."
}

function Get-ProjectVersion {
    [xml]$projectXml = Get-Content -LiteralPath $projectPath
    $versionNode = $projectXml.Project.PropertyGroup |
        Where-Object { $_.Version } |
        Select-Object -First 1

    if ($versionNode -and -not [string]::IsNullOrWhiteSpace($versionNode.Version)) {
        return $versionNode.Version.Trim()
    }

    return "1.0"
}

function Clear-ProjectDirectory {
    param([Parameter(Mandatory)][string]$Path)

    $fullRoot = [System.IO.Path]::GetFullPath($rootDir).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $expectedPrefix = $fullRoot + [System.IO.Path]::DirectorySeparatorChar

    if (-not $fullPath.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a directory outside the project: $fullPath"
    }

    if (Test-Path -LiteralPath $fullPath) {
        for ($attempt = 1; $attempt -le 5; $attempt++) {
            try {
                Remove-Item -LiteralPath $fullPath -Recurse -Force
                break
            }
            catch {
                if ($attempt -ge 5) {
                    throw
                }

                Start-Sleep -Milliseconds 500
            }
        }
    }

    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Project file was not found: $projectPath"
}

if (-not (Test-Path -LiteralPath $installerScriptPath)) {
    throw "Inno Setup script was not found: $installerScriptPath"
}

$dotnetPath = Resolve-DotNet
$innoCompilerPath = Resolve-InnoCompiler -DotNetPath $dotnetPath
$version = Get-ProjectVersion
$installerBaseName = "SPConverter_Setup_v$version"
$zipPath = Join-Path $setupDir "SPConverter_v${version}_Portable.zip"

Write-Host "SP Converter release build"
Write-Host "Version: $version"
Write-Host "Runtime: $runtime"
Write-Host "Output: $setupDir"
Write-Host ""

Clear-ProjectDirectory -Path $publishDir
Clear-ProjectDirectory -Path $setupDir

Push-Location $rootDir
try {
    if (-not $SkipTests) {
        Write-Host "Running tests..."
        & $dotnetPath test $testsPath -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Tests failed."
        }
        Write-Host ""
    }

    Write-Host "Publishing portable app..."
    & $dotnetPath publish $projectPath `
        -c Release `
        -r $runtime `
        --self-contained true `
        -o $publishDir

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed."
    }

    $portableExe = Join-Path $publishDir "SPConverter.exe"
    if (-not (Test-Path -LiteralPath $portableExe)) {
        throw "Published SPConverter.exe was not found: $portableExe"
    }

    Get-ChildItem -LiteralPath $publishDir -Filter "*.pdb" -File |
        Remove-Item -Force

    Write-Host ""

    Write-Host "Building installer..."
    & $innoCompilerPath "/O$setupDir" "/F$installerBaseName" "/DMyAppVersion=$version" $installerScriptPath
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup build failed."
    }
    Write-Host ""

    Write-Host "Creating portable ZIP..."
    Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

    Write-Host ""
    Write-Host "Release files created:"
    Get-ChildItem -LiteralPath $setupDir -File | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
}
finally {
    Pop-Location
}
