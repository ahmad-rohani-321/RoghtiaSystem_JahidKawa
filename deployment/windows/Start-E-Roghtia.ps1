param(
  [switch]$NoBrowser,
  [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$base = Split-Path -Parent $MyInvocation.MyCommand.Path
$server = Join-Path $base 'App\server'
$client = Join-Path $base 'App\client'
$data = Join-Path $base 'Data'
$apiDll = Join-Path $server 'RoghtiaSystem_JahidKawa.Server.dll'
$clientEntry = Join-Path $client 'server\index.mjs'
$nodeExe = Join-Path $base 'App\runtime\node\node.exe'

function Show-StartupLogs {
  foreach ($log in @((Join-Path $data 'server-error.log'), (Join-Path $data 'client-error.log'), (Join-Path $data 'server.log'), (Join-Path $data 'client.log'))) {
    if (Test-Path -LiteralPath $log) {
      Write-Host "`n===== $log (last 60 lines) =====" -ForegroundColor Cyan
      Get-Content -LiteralPath $log -Tail 60
    }
  }
}

trap {
  $message = "E-Roghtia failed to start: $($_.Exception.Message)"
  if ($Quiet) {
    if (Test-Path -LiteralPath $data) { Add-Content -LiteralPath (Join-Path $data 'startup-error.log') -Value "$(Get-Date -Format o) $message" }
    exit 1
  }
  Write-Host "`n$message" -ForegroundColor Red
  Show-StartupLogs
  Write-Host ''
  Read-Host 'Press Enter to close this window'
  exit 1
}

$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:Auth__SigningKey = [Environment]::GetEnvironmentVariable('Auth__SigningKey', 'User')
$env:ConnectionStrings__MainDatabase = [Environment]::GetEnvironmentVariable('ConnectionStrings__MainDatabase', 'User')
if ([string]::IsNullOrWhiteSpace($env:Auth__SigningKey) -or [string]::IsNullOrWhiteSpace($env:ConnectionStrings__MainDatabase)) { throw 'Run Setup E-Roghtia.cmd once before starting.' }
if (-not (Test-Path -LiteralPath $data)) { throw 'Data folder is missing. Run Setup E-Roghtia.cmd once before starting.' }
if (-not (Test-Path -LiteralPath $apiDll)) { throw "Server file not found: $apiDll" }
if (-not (Test-Path -LiteralPath $clientEntry)) { throw "Client file not found: $clientEntry" }
if (-not (Test-Path -LiteralPath $nodeExe)) { throw "Bundled Node.js was not found: $nodeExe" }

function Test-LocalHttp([string]$uri) {
  try {
    $response = Invoke-WebRequest -UseBasicParsing -Uri $uri -TimeoutSec 2 -ErrorAction Stop
    return [int]$response.StatusCode -ge 200 -and [int]$response.StatusCode -lt 300
  } catch { return $false }
}

$apiReady = Test-LocalHttp 'http://127.0.0.1:5201/api/health'
if (-not $apiReady) {
  $env:ASPNETCORE_URLS = 'http://127.0.0.1:5201'
  Remove-Item -LiteralPath (Join-Path $data 'server-error.log') -Force -ErrorAction SilentlyContinue
  $apiArguments = @(('"' + $apiDll + '"'), '--urls', 'http://127.0.0.1:5201')
  $apiProcess = Start-Process -FilePath 'dotnet' -ArgumentList $apiArguments -WorkingDirectory $server -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $data 'server.log') -RedirectStandardError (Join-Path $data 'server-error.log')
  for ($attempt = 0; $attempt -lt 45; $attempt++) {
    Start-Sleep -Seconds 1
    if (Test-LocalHttp 'http://127.0.0.1:5201/api/health') { $apiReady = $true; break }
    if ($apiProcess.HasExited) { throw "The .NET API exited during startup (exit code $($apiProcess.ExitCode))." }
  }
  if (-not $apiReady) { throw 'The .NET API did not become ready at 127.0.0.1:5201.' }
}

$clientReady = Test-LocalHttp 'http://127.0.0.1:43127/login'
if (-not $clientReady) {
  $env:NODE_ENV = 'production'
  $env:HOST = '127.0.0.1'
  $env:NITRO_HOST = '127.0.0.1'
  $env:PORT = '43127'
  $env:NITRO_PORT = '43127'
  $env:NUXT_AUTH_API_BASE = 'http://127.0.0.1:5201'
  $env:NUXT_AUTH_COOKIE_SECURE = 'false'
  Remove-Item -LiteralPath (Join-Path $data 'client-error.log') -Force -ErrorAction SilentlyContinue
  $nodeProcess = Start-Process -FilePath $nodeExe -ArgumentList ('"' + $clientEntry + '"') -WorkingDirectory $client -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $data 'client.log') -RedirectStandardError (Join-Path $data 'client-error.log')
  for ($attempt = 0; $attempt -lt 30; $attempt++) {
    Start-Sleep -Seconds 1
    if (Test-LocalHttp 'http://127.0.0.1:43127/login') { $clientReady = $true; break }
    if ($nodeProcess.HasExited) { throw "The Nuxt client exited during startup (exit code $($nodeProcess.ExitCode))." }
  }
  if (-not $clientReady) { throw 'The Nuxt client did not start listening at 127.0.0.1:43127.' }
}

if (-not $NoBrowser) { Start-Process 'http://localhost:43127/login' }
