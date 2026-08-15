[CmdletBinding()]
param(
    [string]$ComposeFile = "",
    [string]$ApiUrl = "http://127.0.0.1:5188",
    [string]$AliasPrefix = "dockersmoke",
    [switch]$KeepRunning
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

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

function Invoke-DockerCli {
    param(
        [string[]]$Arguments
    )

    $dockerCommand = Get-Command docker.exe -ErrorAction Stop
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $dockerCommand.Source
    $startInfo.Arguments = [string]::Join(" ", ($Arguments | ForEach-Object {
        if ($_ -match '\s|"' ) {
            '"' + ($_ -replace '"', '\"') + '"'
        }
        else {
            $_
        }
    }))

    $startInfo.WorkingDirectory = $repoRoot
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

    return [pscustomobject]@{
        ExitCode = $process.ExitCode
        StdOut = $standardOutput.Trim()
        StdErr = $standardError.Trim()
        Combined = (@($standardOutput.Trim(), $standardError.Trim()) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join [Environment]::NewLine
    }
}

function Test-DockerDaemon {
    param(
        [string]$ComposePath
    )

    $composeVersion = Invoke-DockerCli -Arguments @("compose", "-f", $ComposePath, "version")
    if ($composeVersion.ExitCode -ne 0) {
        throw "Docker Compose is not available from the current shell. Confirm Docker Desktop or the Docker CLI plugin is installed, then rerun the smoke script.`n$($composeVersion.Combined)"
    }

    $serverVersion = Invoke-DockerCli -Arguments @("version", "--format", "{{.Server.Version}}")
    if ($serverVersion.ExitCode -ne 0) {
        throw "Docker daemon is not reachable from the current shell. Start Docker Desktop and ensure this shell can access //./pipe/docker_engine, then rerun the smoke script.`n$($serverVersion.Combined)"
    }
}

$scriptRoot = if ([string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    Split-Path -Parent $PSCommandPath
}
else {
    $PSScriptRoot
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($ComposeFile)) {
    $ComposeFile = Join-Path $repoRoot "docker-compose.yml"
}

$composePath = [System.IO.Path]::GetFullPath($ComposeFile)

if (-not (Test-Path -LiteralPath $composePath)) {
    throw "Compose file not found at $composePath"
}

$client = $null

try {
    Test-DockerDaemon -ComposePath $composePath
    $configResult = Invoke-DockerCli -Arguments @("compose", "-f", $composePath, "config")
    if ($configResult.ExitCode -ne 0) {
        throw "docker compose config failed.`n$($configResult.Combined)"
    }

    $upResult = Invoke-DockerCli -Arguments @("compose", "-f", $composePath, "up", "-d", "--build")
    if ($upResult.ExitCode -ne 0) {
        throw "docker compose up -d --build failed.`n$($upResult.Combined)"
    }

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(5)

    $healthUrl = ($ApiUrl.TrimEnd("/") + "/api/health")
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Seconds 2
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
        $logsResult = Invoke-DockerCli -Arguments @("compose", "-f", $composePath, "logs", "--no-color")
        throw "Composed API did not become ready.`n$($logsResult.Combined)"
    }

    $alias = "$AliasPrefix-$([DateTimeOffset]::UtcNow.ToUnixTimeSeconds())"
    $createResponse = Invoke-Json -Client $client -Method "Post" -Url ($ApiUrl.TrimEnd("/") + "/api/short-links") -Body @{
        originalUrl = "https://example.com/docker-smoke"
        customAlias = $alias
    }

    if ([int]$createResponse.StatusCode -ne 201) {
        throw "Create short link failed with status $([int]$createResponse.StatusCode)."
    }

    $created = $createResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    $detailResponse = Invoke-Json -Client $client -Method "Get" -Url ($ApiUrl.TrimEnd("/") + "/api/short-links/$alias")
    $detail = $detailResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    $redirectResponse = Invoke-Json -Client $client -Method "Get" -Url ($ApiUrl.TrimEnd("/") + "/$alias")
    $deleteResponse = Invoke-Json -Client $client -Method "Delete" -Url ($ApiUrl.TrimEnd("/") + "/api/short-links/$alias")
    $postDeleteRedirect = Invoke-Json -Client $client -Method "Get" -Url ($ApiUrl.TrimEnd("/") + "/$alias")

    [pscustomobject]@{
        Status = "Completed"
        ComposeFile = $composePath
        RepoRoot = $repoRoot
        ApiUrl = $ApiUrl
        Alias = $alias
        CreateStatus = [int]$createResponse.StatusCode
        DetailStatus = [int]$detailResponse.StatusCode
        RedirectStatus = [int]$redirectResponse.StatusCode
        RedirectLocation = $redirectResponse.Headers.Location.AbsoluteUri
        DeleteStatus = [int]$deleteResponse.StatusCode
        PostDeleteRedirectStatus = [int]$postDeleteRedirect.StatusCode
        ShortUrl = $created.shortUrl
        OriginalUrl = $detail.originalUrl
        IsActive = $detail.isActive
    } | ConvertTo-Json -Depth 5
}
finally {
    if ($null -ne $client) {
        $client.Dispose()
    }

    if (-not $KeepRunning) {
        $null = Invoke-DockerCli -Arguments @("compose", "-f", $composePath, "down", "-v")
    }
}
