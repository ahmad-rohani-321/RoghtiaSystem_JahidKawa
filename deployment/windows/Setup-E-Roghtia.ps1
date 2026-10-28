$ErrorActionPreference = 'Stop'
$installRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$dataRoot = Join-Path $installRoot 'Data'
New-Item -ItemType Directory -Force -Path $dataRoot | Out-Null

function New-RandomBase64([int]$length) {
  $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
  try {
    $bytes = New-Object byte[] $length
    $rng.GetBytes($bytes)
    return [Convert]::ToBase64String($bytes)
  } finally { $rng.Dispose() }
}

if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable('Auth__SigningKey', 'User'))) {
  [Environment]::SetEnvironmentVariable('Auth__SigningKey', (New-RandomBase64 64), 'User')
}
if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable('ConnectionStrings__MainDatabase', 'User'))) {
  $databasePath = Join-Path $dataRoot 'E-Roghtia.db'
  $databasePassword = New-RandomBase64 32
  [Environment]::SetEnvironmentVariable('ConnectionStrings__MainDatabase', "Data Source=$databasePath;Mode=ReadWriteCreate;Password=$databasePassword", 'User')
}
[Environment]::SetEnvironmentVariable('ASPNETCORE_ENVIRONMENT', 'Production', 'User')

$startScript = Join-Path $installRoot 'Start-E-Roghtia.ps1'
$currentUser = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$taskName = 'E-Roghtia Start at Sign-in'
$taskArguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "{0}" -NoBrowser -Quiet' -f $startScript
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $taskArguments -WorkingDirectory $installRoot
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $currentUser
$principal = New-ScheduledTaskPrincipal -UserId $currentUser -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit ([TimeSpan]::Zero) -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description 'Starts the local E-Roghtia services when this Windows user signs in.' -Force | Out-Null

Write-Host 'E-Roghtia is set up for this Windows user.' -ForegroundColor Green
Write-Host 'The app will start in the background each time this user signs in.'
Write-Host 'Use Start E-Roghtia.cmd to start it now and open the app.'
