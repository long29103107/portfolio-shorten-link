[CmdletBinding()]
param(
    [string]$PackageVersion = "1.0.0",
    [string]$OutputDirectory = "",
    [switch]$KeepArtifacts,
    [switch]$Publish
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($Publish) {
    throw "This dry-run script never publishes packages. Use a future explicit publish workflow with credentials and manual approval."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

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
        [string]$WorkingDirectory
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
        throw "$FileName $($startInfo.Arguments) failed with exit code $($process.ExitCode).`n$combined"
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
        throw "Refusing to clean or write outside ${resolvedParent}: $resolvedPath"
    }
}

function Get-NuspecText {
    param(
        [xml]$Nuspec,
        [string]$ElementName
    )

    $element = $Nuspec.GetElementsByTagName($ElementName) | Select-Object -First 1
    if ($null -eq $element) {
        return ""
    }

    return [string]$element.InnerText
}

function Assert-NuspecText {
    param(
        [xml]$Nuspec,
        [string]$ElementName,
        [string]$ExpectedValue,
        [string]$PackageId
    )

    $actual = Get-NuspecText -Nuspec $Nuspec -ElementName $ElementName
    if ($actual -ne $ExpectedValue) {
        throw "$PackageId has unexpected ${ElementName}. Expected '$ExpectedValue', got '$actual'."
    }
}

function Get-NuspecDependencyIds {
    param([xml]$Nuspec)

    return @($Nuspec.GetElementsByTagName("dependency") | ForEach-Object {
        $_.GetAttribute("id")
    } | Where-Object {
        -not [string]::IsNullOrWhiteSpace($_)
    })
}

function Inspect-Package {
    param(
        [string]$PackageId,
        [string]$PackagePath,
        [string[]]$RequiredDependencies = @()
    )

    if (-not (Test-Path -LiteralPath $PackagePath)) {
        throw "Expected package was not produced: $PackagePath"
    }

    $zip = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entryNames = @($zip.Entries | ForEach-Object { $_.FullName })
        $nuspecEntry = $zip.Entries | Where-Object { $_.FullName -like "*.nuspec" } | Select-Object -First 1
        if ($null -eq $nuspecEntry) {
            throw "$PackageId package does not include a nuspec file."
        }

        $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
        try {
            [xml]$nuspec = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }

        Assert-NuspecText -Nuspec $nuspec -ElementName "id" -ExpectedValue $PackageId -PackageId $PackageId
        Assert-NuspecText -Nuspec $nuspec -ElementName "version" -ExpectedValue $PackageVersion -PackageId $PackageId
        Assert-NuspecText -Nuspec $nuspec -ElementName "authors" -ExpectedValue "ShortenLink Contributors" -PackageId $PackageId
        Assert-NuspecText -Nuspec $nuspec -ElementName "readme" -ExpectedValue "README.md" -PackageId $PackageId

        $description = Get-NuspecText -Nuspec $nuspec -ElementName "description"
        if ([string]::IsNullOrWhiteSpace($description) -or $description -notmatch "Shorten Link") {
            throw "$PackageId has an empty or off-brand description: '$description'."
        }

        $tags = Get-NuspecText -Nuspec $nuspec -ElementName "tags"
        if ($tags -notmatch "short-link" -or $tags -notmatch "url-shortener") {
            throw "$PackageId tags must include short-link and url-shortener. Got '$tags'."
        }

        $license = $nuspec.GetElementsByTagName("license") | Select-Object -First 1
        if ($null -eq $license -or $license.GetAttribute("type") -ne "expression" -or $license.InnerText -ne "MIT") {
            throw "$PackageId must declare MIT as a license expression."
        }

        $repository = $nuspec.GetElementsByTagName("repository") | Select-Object -First 1
        if ($null -eq $repository -or $repository.GetAttribute("type") -ne "git" -or $repository.GetAttribute("url") -ne "https://github.com/long29103107/shorten-link") {
            throw "$PackageId must include the expected git repository metadata."
        }

        if ($entryNames -notcontains "README.md") {
            throw "$PackageId package does not include README.md at the package root."
        }

        if (-not ($entryNames | Where-Object { $_ -eq "lib/net10.0/$PackageId.dll" })) {
            throw "$PackageId package does not include lib/net10.0/$PackageId.dll."
        }

        if ($entryNames | Where-Object { $_ -match "ShortenLink\.(Api|Web)" }) {
            throw "$PackageId package contains demo API/Web artifacts."
        }

        $dependencyIds = @(Get-NuspecDependencyIds -Nuspec $nuspec)
        foreach ($dependency in $RequiredDependencies) {
            if ($dependencyIds -notcontains $dependency) {
                throw "$PackageId is missing required dependency '$dependency'. Dependencies: $($dependencyIds -join ', ')"
            }
        }

        if ($dependencyIds | Where-Object { $_ -match "^ShortenLink\.(Api|Web)$" }) {
            throw "$PackageId depends on demo API/Web packages."
        }

        return [pscustomobject]@{
            PackageId = $PackageId
            Path = $PackagePath
            Version = Get-NuspecText -Nuspec $nuspec -ElementName "version"
            Description = $description
            Tags = $tags
            DependencyIds = $dependencyIds
            ReadmeIncluded = $true
            DemoCouplingFound = $false
        }
    }
    finally {
        $zip.Dispose()
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
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $tempRoot "release-dry-run"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
Assert-UnderDirectory -Path $OutputDirectory -Parent $tempRoot

if (Test-Path -LiteralPath $OutputDirectory) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
Invoke-Tool -FileName $dotnet -WorkingDirectory $repoRoot -Arguments @(
    "pack",
    "ShortenLink.slnx",
    "-c",
    "Release",
    "-o",
    $OutputDirectory,
    "--verbosity",
    "minimal"
) | Out-Null

$results = @(
    Inspect-Package -PackageId "ShortenLink.Auditing" -PackagePath (Join-Path $OutputDirectory "ShortenLink.Auditing.$PackageVersion.nupkg")
    Inspect-Package -PackageId "ShortenLink.Core" -PackagePath (Join-Path $OutputDirectory "ShortenLink.Core.$PackageVersion.nupkg")
    Inspect-Package -PackageId "ShortenLink.Infrastructure" -PackagePath (Join-Path $OutputDirectory "ShortenLink.Infrastructure.$PackageVersion.nupkg") -RequiredDependencies @("ShortenLink.Auditing", "ShortenLink.Core")
    Inspect-Package -PackageId "ShortenLink.Hosting" -PackagePath (Join-Path $OutputDirectory "ShortenLink.Hosting.$PackageVersion.nupkg") -RequiredDependencies @("ShortenLink.Auditing", "ShortenLink.Core", "ShortenLink.Application", "ShortenLink.Infrastructure")
)

$summary = [pscustomobject]@{
    Status = "Completed"
    Mode = "DryRunOnly"
    Published = $false
    OutputDirectory = $OutputDirectory
    PackageVersion = $PackageVersion
    Packages = $results
    PublishGuard = "This script never calls dotnet nuget push and rejects -Publish."
}

$summary | ConvertTo-Json -Depth 10

if (-not $KeepArtifacts) {
    Remove-Item -LiteralPath $OutputDirectory -Recurse -Force
}
