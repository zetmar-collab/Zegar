param([switch]$NoLaunch, [switch]$EnableAutostart, [switch]$DisableAutostart)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'AutostartPolicy.ps1')
if ($EnableAutostart -and $DisableAutostart) { throw 'Wybierz tylko jedną opcję autostartu.' }
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../publish'))
$target = Join-Path $env:LOCALAPPDATA 'Programs/ZegarHTC'
$isUpgrade = Test-Path -LiteralPath (Join-Path $target 'Zegar.exe')
$run = 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
$previousAutostart = Get-ItemPropertyValue -Path $run -Name ZegarHTC -ErrorAction SilentlyContinue
if (-not (Test-Path -LiteralPath (Join-Path $source 'Zegar.exe'))) { throw 'Najpierw zbuduj aplikację poleceniem scripts/Build.ps1.' }
New-Item -ItemType Directory -Path $target -Force | Out-Null
Get-Process -Name Zegar -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $target 'Zegar.exe') } | ForEach-Object {
    if (-not $_.CloseMainWindow()) { $_.Kill() }
    if (-not $_.WaitForExit(4000)) { $_.Kill(); $_.WaitForExit() }
}
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force
$executable = Join-Path $target 'Zegar.exe'
$shell = New-Object -ComObject WScript.Shell
$desktop = [Environment]::GetFolderPath('Desktop')
$programs = [Environment]::GetFolderPath('Programs')
foreach ($folder in @($desktop, $programs)) {
    $shortcut = $shell.CreateShortcut((Join-Path $folder 'Zegar HTC.lnk'))
    $shortcut.TargetPath = $executable
    $shortcut.WorkingDirectory = $target
    $shortcut.Description = 'Zegar 12/24 h, data i pogoda Open-Meteo'
    $shortcut.Save()
}
$restart = $shell.CreateShortcut((Join-Path $desktop 'Restart zegara HTC.lnk'))
$restart.TargetPath = $executable
$restart.Arguments = '--restart'
$restart.WorkingDirectory = $target
$restart.Description = 'Uruchom ponownie również zawieszony widżet'
$restart.Save()
New-Item -Path $run -Force | Out-Null
$startupDecision = Get-AutostartDecision -IsUpgrade $isUpgrade -HasEntry ([bool]$previousAutostart) -Enable ([bool]$EnableAutostart) -Disable ([bool]$DisableAutostart)
if ($startupDecision -eq 'Disable') { Remove-ItemProperty -Path $run -Name ZegarHTC -ErrorAction SilentlyContinue }
elseif ($startupDecision -eq 'Enable') { Set-ItemProperty -Path $run -Name 'ZegarHTC' -Value ('"' + $executable + '"') }
Write-Output "Zainstalowano: $executable. Zachowano preferencje autostartu podczas aktualizacji."
if (-not $NoLaunch) { Start-Process -FilePath $executable -WorkingDirectory $target }
