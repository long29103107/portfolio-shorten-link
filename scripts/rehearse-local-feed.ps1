[CmdletBinding()]
param(
    [string]$PackageVersion = "1.0.0",
    [string]$FeedDirectory = "",
    [string]$WorkDirectory = "",
    [string]$ApiUrl = "http://127.0.0.1:5299",
    [switch]$ResetFeed,
    [switch]$SkipDuplicate,
    [switch]$KeepArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Assert-UnderDirectory {
    param(
        [string]$Path,
        [string]$Parent
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $resolvedParent = [System.IO.Path]::GetFullPath($Parent)
    if (-not $resolvedPath.StartsWith($resolvedParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write rehearsal artifacts outside ${resolvedParent}: $resolvedPath"
    }
}

$scriptRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    Split-Path -Parent $PSCommandPath
}
else {
    $PSScriptRoot
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot ".."))
$tempRoot = Join-Path $repoRoot ".tmp"
if ([string]::IsNullOrWhiteSpace($FeedDirectory)) {
    $FeedDirectory = Join-Path $tempRoot "local-nuget-feed"
}

if ([string]::IsNullOrWhiteSpace($WorkDirectory)) {
    $WorkDirectory = Join-Path $tempRoot "local-feed-rehearsal"
}

$FeedDirectory = [System.IO.Path]::GetFullPath($FeedDirectory)
$WorkDirectory = [System.IO.Path]::GetFullPath($WorkDirectory)
Assert-UnderDirectory -Path $FeedDirectory -Parent $tempRoot
Assert-UnderDirectory -Path $WorkDirectory -Parent $tempRoot

$packageIds = @(
    "ShortenLink.Auditing",
    "ShortenLink.Core",
    "ShortenLink.Infrastructure",
    "ShortenLink.Hosting"
)

if ($ResetFeed -and (Test-Path -LiteralPath $FeedDirectory)) {
    Remove-Item -LiteralPath $FeedDirectory -Recurse -Force
}

if (Test-Path -LiteralPath $WorkDirectory) {
    Remove-Item -LiteralPath $WorkDirectory -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $FeedDirectory | Out-Null
New-Item -ItemType Directory -Force -Path $WorkDirectory | Out-Null

$validatedPackages = Join-Path $WorkDirectory "validated-packages"
$releaseDryRun = Join-Path $scriptRoot "release-dry-run.ps1"
$consumerSmoke = Join-Path $scriptRoot "smoke-consumer-package.ps1"

if (-not (Test-Path -LiteralPath $releaseDryRun)) {
    throw "Missing release dry-run script: $releaseDryRun"
}

if (-not (Test-Path -LiteralPath $consumerSmoke)) {
    throw "Missing consumer smoke script: $consumerSmoke"
}

& $releaseDryRun -PackageVersion $PackageVersion -OutputDirectory $validatedPackages -KeepArtifacts | Out-Null

$copied = @()
$skipped = @()
foreach ($packageId in $packageIds) {
    $sourcePackage = Join-Path $validatedPackages "$packageId.$PackageVersion.nupkg"
    $feedPackage = Join-Path $FeedDirectory "$packageId.$PackageVersion.nupkg"

    if (-not (Test-Path -LiteralPath $sourcePackage)) {
        throw "Validated package was not produced: $sourcePackage"
    }

    if (Test-Path -LiteralPath $feedPackage) {
        if ($SkipDuplicate) {
            $skipped += $feedPackage
            continue
        }

        throw "Local rehearsal feed already contains $feedPackage. Use -ResetFeed to start clean or -SkipDuplicate to rehearse an intentional retry."
    }

    Copy-Item -LiteralPath $sourcePackage -Destination $feedPackage
    $copied += $feedPackage
}

foreach ($packageId in $packageIds) {
    $feedPackage = Join-Path $FeedDirectory "$packageId.$PackageVersion.nupkg"
    if (-not (Test-Path -LiteralPath $feedPackage)) {
        throw "Rehearsal feed is missing package: $feedPackage"
    }
}

$consumerRoot = Join-Path $WorkDirectory "consumer-smoke"
$smokeJson = & $consumerSmoke -PackageVersion $PackageVersion -PackageSource $FeedDirectory -ConsumerRoot $consumerRoot -ApiUrl $ApiUrl -UseExistingPackageSource
$smoke = $smokeJson | ConvertFrom-Json

$summary = [pscustomobject]@{
    Status = "Completed"
    Mode = "LocalFeedRehearsal"
    PublishedToNuGetOrg = $false
    RequiresCredentials = $false
    PackageVersion = $PackageVersion
    FeedDirectory = $FeedDirectory
    ValidatedPackageDirectory = $validatedPackages
    CopiedPackages = $copied
    SkippedDuplicatePackages = $skipped
    ConsumerSmoke = $smoke
    DuplicatePolicy = "Use -ResetFeed to start clean, or -SkipDuplicate to rehearse an intentional retry."
}

$summary | ConvertTo-Json -Depth 10

if (-not $KeepArtifacts) {
    if (Test-Path -LiteralPath $WorkDirectory) {
        Remove-Item -LiteralPath $WorkDirectory -Recurse -Force
    }
}
