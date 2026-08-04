[CmdletBinding()]
param(
    [string]$PackageVersion = "1.0.1",
    [string]$ApiUrl = "http://127.0.0.1:5298",
    [string]$PackageSource = "",
    [string]$ConsumerRoot = "",
    [switch]$UseExistingPackageSource,
    [switch]$KeepArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

function Join-ProcessArguments {
    param([string[]]$Arguments)

    return [string]::Join(" ", ($Arguments | ForEach-Object {
        if ($_ -match '\s|"' ) {
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

function Invoke-Json {
    param(
        [System.Net.Http.HttpClient]$Client,
        [string]$Method,
        [string]$Url,
        [object]$Body = $null
    )

    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::$Method, $Url)
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 10
        $request.Content = [System.Net.Http.StringContent]::new($json, [System.Text.Encoding]::UTF8, "application/json")
    }

    return $Client.SendAsync($request).GetAwaiter().GetResult()
}

function Assert-UnderDirectory {
    param(
        [string]$Path,
        [string]$Parent
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    $resolvedParent = [System.IO.Path]::GetFullPath($Parent)
    if (-not $resolvedPath.StartsWith($resolvedParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean path outside ${resolvedParent}: $resolvedPath"
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
if ([string]::IsNullOrWhiteSpace($PackageSource)) {
    $PackageSource = Join-Path $tempRoot "consumer-packages"
}

if ([string]::IsNullOrWhiteSpace($ConsumerRoot)) {
    $ConsumerRoot = Join-Path $tempRoot "consumer-smoke"
}

$PackageSource = [System.IO.Path]::GetFullPath($PackageSource)
$ConsumerRoot = [System.IO.Path]::GetFullPath($ConsumerRoot)
Assert-UnderDirectory -Path $PackageSource -Parent $tempRoot
Assert-UnderDirectory -Path $ConsumerRoot -Parent $tempRoot

$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$apiProcess = $null
$client = $null

try {
    New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null
    if ((Test-Path -LiteralPath $PackageSource) -and -not $UseExistingPackageSource) {
        Remove-Item -LiteralPath $PackageSource -Recurse -Force
    }

    if (Test-Path -LiteralPath $ConsumerRoot) {
        Remove-Item -LiteralPath $ConsumerRoot -Recurse -Force
    }

    if ($UseExistingPackageSource) {
        if (-not (Test-Path -LiteralPath $PackageSource)) {
            throw "Existing package source was not found: $PackageSource"
        }
    }
    else {
        New-Item -ItemType Directory -Force -Path $PackageSource | Out-Null

        Invoke-Tool -FileName $dotnet -WorkingDirectory $repoRoot -Arguments @(
            "restore",
            "ShortenLink.slnx",
            "--ignore-failed-sources",
            "--verbosity",
            "minimal"
        ) | Out-Null

        Invoke-Tool -FileName $dotnet -WorkingDirectory $repoRoot -Arguments @(
            "pack",
            "ShortenLink.slnx",
            "-c",
            "Release",
            "-o",
            $PackageSource,
            "-p:PackageVersion=$PackageVersion",
            "--no-restore",
            "--verbosity",
            "minimal"
        ) | Out-Null
    }

    foreach ($packageId in @("ShortenLink.Auditing", "ShortenLink.Core", "ShortenLink.Infrastructure", "ShortenLink.Hosting")) {
        $packagePath = Join-Path $PackageSource "$packageId.$PackageVersion.nupkg"
        if (-not (Test-Path -LiteralPath $packagePath)) {
            throw "Expected local package was not produced: $packagePath"
        }
    }

    Invoke-Tool -FileName $dotnet -WorkingDirectory $repoRoot -Arguments @(
        "new",
        "web",
        "--framework",
        "net10.0",
        "--no-restore",
        "--output",
        $ConsumerRoot
    ) | Out-Null

    # Keep the generated consumer isolated from repository-wide implicit
    # global usings and project conventions while it verifies NuGet usage.
    @'
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
'@ | Set-Content -LiteralPath (Join-Path $ConsumerRoot "Directory.Build.props") -Encoding UTF8

    Invoke-Tool -FileName $dotnet -WorkingDirectory $ConsumerRoot -Arguments @(
        "add",
        "package",
        "ShortenLink.Hosting",
        "--version",
        $PackageVersion,
        "--no-restore"
    ) | Out-Null

    $projectFile = Join-Path $ConsumerRoot "consumer-smoke.csproj"
    if (-not (Test-Path -LiteralPath $projectFile)) {
        $projectFile = Get-ChildItem -LiteralPath $ConsumerRoot -Filter "*.csproj" | Select-Object -First 1 -ExpandProperty FullName
    }

    $projectText = Get-Content -LiteralPath $projectFile -Raw
    if ($projectText -notmatch 'PackageReference Include="ShortenLink.Hosting"') {
        throw "Consumer project does not reference the ShortenLink.Hosting package."
    }

    if ($projectText -match "ProjectReference|ShortenLink.Api") {
        throw "Consumer project must not use project references or ShortenLink.Api internals."
    }

    $programPath = Join-Path $ConsumerRoot "Program.cs"
    @'
using ShortenLink.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddShortenLink(builder.Configuration);

var app = builder.Build();

app.UseRateLimiter();
app.MapShortenLinkEndpoints();
app.MapGet("/consumer-health", () => Results.Ok(new { status = "ok", app = "consumer-smoke" }));

app.Run();
'@ | Set-Content -LiteralPath $programPath -Encoding UTF8

    Invoke-Tool -FileName $dotnet -WorkingDirectory $ConsumerRoot -Arguments @(
        "restore",
        "--source",
        $PackageSource,
        "--ignore-failed-sources",
        "--force-evaluate",
        "--verbosity",
        "minimal"
    ) | Out-Null
    Invoke-Tool -FileName $dotnet -WorkingDirectory $ConsumerRoot -Arguments @("build", "--no-restore", "--verbosity", "minimal") | Out-Null

    $databasePath = Join-Path $ConsumerRoot "consumer-smoke.db"
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $dotnet
    $startInfo.Arguments = Join-ProcessArguments -Arguments @("run", "--no-build", "--no-launch-profile", "--urls", $ApiUrl)
    $startInfo.WorkingDirectory = $ConsumerRoot
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production"
    $startInfo.EnvironmentVariables["ShortenLink__BaseUrl"] = ($ApiUrl.TrimEnd("/") + "/")
    $startInfo.EnvironmentVariables["ShortenLink__Database__UsePostgres"] = "false"
    $startInfo.EnvironmentVariables["ShortenLink__Database__SqliteConnectionString"] = "Data Source=$databasePath"
    $startInfo.EnvironmentVariables["ShortenLink__Redirect__EnableFrontendFallback"] = "false"
    $startInfo.EnvironmentVariables["ShortenLink__Analytics__Enabled"] = "false"
    $startInfo.EnvironmentVariables["ShortenLink__Analytics__UseAsyncWorker"] = "true"
    $startInfo.EnvironmentVariables["ShortenLink__Analytics__QueueCapacity"] = "32"
    $startInfo.EnvironmentVariables["ShortenLink__Cache__Enabled"] = "false"
    $startInfo.EnvironmentVariables["ShortenLink__Cache__Provider"] = "Memory"
    $startInfo.EnvironmentVariables["ShortenLink__Cache__RedisConnectionString"] = "localhost:6379"
    $startInfo.EnvironmentVariables["ShortenLink__Cache__EntryTtlSeconds"] = "300"
    $startInfo.EnvironmentVariables["ShortenLink__RateLimiting__Enabled"] = "false"
    $startInfo.EnvironmentVariables["ShortenLink__RateLimiting__Create__PermitLimit"] = "60"
    $startInfo.EnvironmentVariables["ShortenLink__RateLimiting__Create__WindowSeconds"] = "60"
    $startInfo.EnvironmentVariables["ShortenLink__RateLimiting__Create__QueueLimit"] = "0"
    $startInfo.EnvironmentVariables["ShortenLink__RateLimiting__Redirect__PermitLimit"] = "120"
    $startInfo.EnvironmentVariables["ShortenLink__RateLimiting__Redirect__WindowSeconds"] = "60"
    $startInfo.EnvironmentVariables["ShortenLink__RateLimiting__Redirect__QueueLimit"] = "0"

    $apiProcess = [System.Diagnostics.Process]::new()
    $apiProcess.StartInfo = $startInfo
    [void]$apiProcess.Start()

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(5)

    $healthUrl = ($ApiUrl.TrimEnd("/") + "/consumer-health")
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 500
        if ($apiProcess.HasExited) {
            break
        }

        try {
            $healthResponse = Invoke-Json -Client $client -Method "Get" -Url $healthUrl
            if ($healthResponse.IsSuccessStatusCode) {
                $ready = $true
                break
            }
        }
        catch {
        }
    }

    if (-not $ready) {
        $stdout = $apiProcess.StandardOutput.ReadToEnd()
        $stderr = $apiProcess.StandardError.ReadToEnd()
        throw "Consumer app did not become ready.`nSTDOUT:`n$stdout`nSTDERR:`n$stderr"
    }

    $expiry = (Get-Date).ToUniversalTime().AddHours(1).ToString("o")
    $createResponse = Invoke-Json -Client $client -Method "Post" -Url ($ApiUrl.TrimEnd("/") + "/api/short-links") -Body @{
        originalUrl = "https://example.com/consumer-smoke"
        expiredAtUtc = $expiry
    }

    if ([int]$createResponse.StatusCode -ne 201) {
        throw "Create short link failed with status $([int]$createResponse.StatusCode)."
    }

    $created = $createResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    $detailResponse = Invoke-Json -Client $client -Method "Get" -Url ($ApiUrl.TrimEnd("/") + "/api/short-links/$($created.code)")
    $detail = $detailResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    $redirectResponse = Invoke-Json -Client $client -Method "Get" -Url ($ApiUrl.TrimEnd("/") + "/$($created.code)")
    $deleteResponse = Invoke-Json -Client $client -Method "Delete" -Url ($ApiUrl.TrimEnd("/") + "/api/short-links/$($created.code)")
    $postDeleteRedirect = Invoke-Json -Client $client -Method "Get" -Url ($ApiUrl.TrimEnd("/") + "/$($created.code)")

    if ([int]$detailResponse.StatusCode -ne 200) {
        throw "Get detail failed with status $([int]$detailResponse.StatusCode)."
    }

    if ([int]$redirectResponse.StatusCode -ne 302) {
        throw "Redirect failed with status $([int]$redirectResponse.StatusCode)."
    }

    if ($redirectResponse.Headers.Location.AbsoluteUri -ne "https://example.com/consumer-smoke") {
        throw "Redirect location mismatch: $($redirectResponse.Headers.Location.AbsoluteUri)"
    }

    if ([int]$deleteResponse.StatusCode -ne 200) {
        throw "Delete short link failed with status $([int]$deleteResponse.StatusCode)."
    }

    if ([int]$postDeleteRedirect.StatusCode -ne 404) {
        throw "Post-delete redirect should be not found, got status $([int]$postDeleteRedirect.StatusCode)."
    }

    [pscustomobject]@{
        Status = "Completed"
        ConsumerRoot = $ConsumerRoot
        PackageSource = $PackageSource
        PackageSourceMode = if ($UseExistingPackageSource) { "Existing" } else { "Generated" }
        ApiUrl = $ApiUrl
        Package = "ShortenLink.Hosting"
        PackageVersion = $PackageVersion
        Code = $created.code
        CreateStatus = [int]$createResponse.StatusCode
        DetailStatus = [int]$detailResponse.StatusCode
        RedirectStatus = [int]$redirectResponse.StatusCode
        RedirectLocation = $redirectResponse.Headers.Location.AbsoluteUri
        DeleteStatus = [int]$deleteResponse.StatusCode
        PostDeleteRedirectStatus = [int]$postDeleteRedirect.StatusCode
        ShortUrl = $created.shortUrl
        OriginalUrl = $detail.originalUrl
        IsActiveBeforeDelete = $detail.isActive
    } | ConvertTo-Json -Depth 5
}
finally {
    if ($null -ne $client) {
        $client.Dispose()
    }

    if ($null -ne $apiProcess -and -not $apiProcess.HasExited) {
        $apiProcess.Kill()
        $apiProcess.WaitForExit()
    }

    if (-not $KeepArtifacts) {
        if (Test-Path -LiteralPath $ConsumerRoot) {
            Remove-Item -LiteralPath $ConsumerRoot -Recurse -Force
        }

        if ((Test-Path -LiteralPath $PackageSource) -and -not $UseExistingPackageSource) {
            Remove-Item -LiteralPath $PackageSource -Recurse -Force
        }
    }
}
