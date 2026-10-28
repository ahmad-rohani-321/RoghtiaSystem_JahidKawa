$ErrorActionPreference = 'Stop'
$installRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiDll = Join-Path $installRoot 'App\server\RoghtiaSystem_JahidKawa.Server.dll'
$clientEntry = Join-Path $installRoot 'App\client\server\index.mjs'
$processes = Get-CimInstance Win32_Process | Where-Object {
  ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -and $_.CommandLine.IndexOf($apiDll, [StringComparison]::OrdinalIgnoreCase) -ge 0) -or
  ($_.Name -eq 'node.exe' -and $_.CommandLine -and $_.CommandLine.IndexOf($clientEntry, [StringComparison]::OrdinalIgnoreCase) -ge 0)
}
foreach ($process in $processes) { Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue }
Write-Host 'E-Roghtia services stopped.' -ForegroundColor Green
