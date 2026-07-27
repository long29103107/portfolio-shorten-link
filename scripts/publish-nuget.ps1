[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$PackageVersion,

    [string]$Source = "https://api.nuget.org/v3/index.json",
    [string]$PackageDirectory = "",
    [string]$NuGetApiKey = $env:NUGET_API_KEY,
    [switch]$Publish,
    [switch]$SkipDuplicate
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Join-ProcessArguments {
    param([string[]]$Arguments)

    return [string]::Join(" ", ($Arguments | ForEach-Object {
        if ($_ -match '\s|"') {
            '"' + ($_ -replace '"', '\"') + '"'
        }
        else {
            $_
        }
    }))
}

function Invoke-Tool {
    param(
        [string]$FileName,
        [string[]]$Arguments,
        [string]$WorkingDirectory,
        [string[]]$DisplayArguments = $Arguments
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FileName
    $startInfo.Arguments = Join-ProcessArguments -Arguments $Arguments
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    [void]$process.Start()
    $standardOutput = $process.StandardOutput.ReadToEnd()
    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    $combined = (@($standardOutput.Trim(), $standardError.Trim()) | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_)
    }) -join [Environment]::NewLine

    if ($process.ExitCode -ne 0) {
        $display = Join-ProcessArguments -Arguments $DisplayArguments
        throw "$FileName $display failed with exit code $($process.ExitCode).`n$combined"
    }

    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        StdOut = $standardOutput.Trim()
        StdErr = $standardError.Trim()
        Combined = $combined
    }
}

function Assert-UnderDirectory {
    param(
        [string]$Path,
        [string]$Parent
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $resolvedParent = [System.IO.Path]::GetFullPath($Parent)
    if (-not $resolvedPath.StartsWith($resolvedParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write package artifacts outside ${resolvedParent}: $resolvedPath"
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
if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $PackageDirectory = Join-Path $tempRoot "nuget-publish"
}

$PackageDirectory = [System.IO.Path]::GetFullPath($PackageDirectory)
Assert-UnderDirectory -Path $PackageDirectory -Parent $tempRoot

$packageIds = @(
    "ShortenLink.Core",
    "ShortenLink.Infrastructure",
    "ShortenLink.Hosting"
)

if (-not $Publish) {
    [pscustomobject]@{
        Status = "PreviewOnly"
        Published = $false
        PackageVersion = $PackageVersion
        Source = $Source
        PackageDirectory = $PackageDirectory
        Packages = $packageIds
        RequiredIntent = "Re-run with -Publish after release verification passes."
        RequiredCredential = "Set NUGET_API_KEY or pass -NuGetApiKey from a secret store."
    } | ConvertTo-Json -Depth 5
    return
}

if ([string]::IsNullOrWhiteSpace($NuGetApiKey)) {
    throw "Publishing requires a NuGet API key. Set NUGET_API_KEY or pass -NuGetApiKey from a secret store."
}

$releaseDryRun = Join-Path $scriptRoot "release-dry-run.ps1"
if (-not (Test-Path -LiteralPath $releaseDryRun)) {
    throw "Missing release dry-run script: $releaseDryRun"
}

& $releaseDryRun -PackageVersion $PackageVersion -OutputDirectory $PackageDirectory -KeepArtifacts | Out-Null

$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$pushed = @()
foreach ($packageId in $packageIds) {
    $packagePath = Join-Path $PackageDirectory "$packageId.$PackageVersion.nupkg"
    if (-not (Test-Path -LiteralPath $packagePath)) {
        throw "Expected validated package is missing: $packagePath"
    }

    $arguments = @(
        "nuget",
        "push",
        $packagePath,
        "--api-key",
        $NuGetApiKey,
        "--source",
        $Source
    )

    $displayArguments = @(
        "nuget",
        "push",
        $packagePath,
        "--api-key",
        "***",
        "--source",
        $Source
    )

    if ($SkipDuplicate) {
        $arguments += "--skip-duplicate"
        $displayArguments += "--skip-duplicate"
    }

    Invoke-Tool -FileName $dotnet -WorkingDirectory $repoRoot -Arguments $arguments -DisplayArguments $displayArguments | Out-Null
    $pushed += $packagePath
}

[pscustomobject]@{
    Status = "Completed"
    Published = $true
    PackageVersion = $PackageVersion
    Source = $Source
    PackageDirectory = $PackageDirectory
    Packages = $pushed
    Guardrails = @(
        "Requires -Publish",
        "Requires NUGET_API_KEY or -NuGetApiKey",
        "Runs release-dry-run before dotnet nuget push"
    )
} | ConvertTo-Json -Depth 5
