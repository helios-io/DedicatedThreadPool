#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build orchestration for Helios.DedicatedThreadPool (replaces the legacy FAKE build.fsx).
.DESCRIPTION
    Cross-platform pwsh build script. Version is read from RELEASE_NOTES.md.
.EXAMPLE
    ./build.ps1 Build
    ./build.ps1 Test
    ./build.ps1 Pack
    ./build.ps1 All
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('Restore', 'Build', 'Test', 'Pack', 'Benchmark', 'All')]
    [string]$Target = 'Build',

    [string]$Configuration = 'Release',

    # Pass to Benchmark: runs --job dry (one iteration, no statistics) for CI smoke validation.
    [switch]$Smoke
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot     = $PSScriptRoot
$SrcDir       = Join-Path $RepoRoot 'src'
$Solution     = Join-Path $SrcDir  'Helios.DedicatedThreadPool.slnx'
$ArtifactsDir = Join-Path $RepoRoot 'bin'
$NuGetDir     = Join-Path $ArtifactsDir 'nuget'
$TestResults  = Join-Path $RepoRoot 'TestResults'
$RunSettings  = Join-Path $RepoRoot 'coverlet.runsettings'

function Get-ReleaseVersion {
    # First "#### <version> <date>" heading in RELEASE_NOTES.md is the current version.
    $notes = Join-Path $RepoRoot 'RELEASE_NOTES.md'
    foreach ($line in Get-Content $notes) {
        if ($line -match '^####\s+(?<v>\S+)') { return $Matches['v'] }
    }
    throw "Could not parse a version from RELEASE_NOTES.md (expected a '#### <version> <date>' heading)."
}

function Invoke-Dotnet {
    # Simple (non-advanced) function: all tokens flow into the automatic $args,
    # so dotnet flags like -o/-c are never mistaken for PowerShell parameters.
    Write-Host "dotnet $($args -join ' ')" -ForegroundColor Cyan
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args -join ' ') failed with exit code $LASTEXITCODE." }
}

function Target-Restore {
    Invoke-Dotnet tool restore
    Invoke-Dotnet restore $Solution
}

function Target-Build {
    Target-Restore
    Invoke-Dotnet build $Solution -c $Configuration --no-restore
}

function Target-Test {
    Target-Build
    Invoke-Dotnet test $Solution -c $Configuration --no-build `
        --logger 'trx' --logger 'console;verbosity=normal' `
        --results-directory $TestResults `
        --collect 'XPlat Code Coverage' --settings $RunSettings
}

function Target-Pack {
    $version = Get-ReleaseVersion
    Write-Host "Packing version $version" -ForegroundColor Green
    Target-Build
    # Build the -p:Key=Value token as a single string so PowerShell's -name:value
    # colon parsing doesn't split it before dotnet sees it.
    $versionArg = "-p:PackageVersion=$version"
    Invoke-Dotnet pack $Solution -c $Configuration --no-build $versionArg -o $NuGetDir
}

function Target-Benchmark {
    Target-Build
    # Pass --job dry for CI smoke (one iteration, no statistics). Full run omits this.
    $bdn = if ($Smoke -or $env:CI) { @('--', '--job', 'dry') } else { @() }
    $benchProjects = Get-ChildItem -Path $SrcDir -Recurse -Filter '*.Benchmarks.csproj'
    foreach ($proj in $benchProjects) {
        Invoke-Dotnet run --project $proj.FullName -c $Configuration --no-build @bdn
    }
}

switch ($Target) {
    'Restore'   { Target-Restore }
    'Build'     { Target-Build }
    'Test'      { Target-Test }
    'Pack'      { Target-Pack }
    'Benchmark' { Target-Benchmark }
    'All'       { Target-Test; Target-Pack }
}

Write-Host "`n$Target complete." -ForegroundColor Green
