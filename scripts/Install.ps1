param([switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
$source = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../publish'))
$target = Join-Path $env:LOCALAPPDATA 'Programs/ZegarHTC'
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
    $shortcut.Description = 'Zegar 24 h, data i pogoda Open-Meteo'
    $shortcut.Save()
}
$restart = $shell.CreateShortcut((Join-Path $desktop 'Restart zegara HTC.lnk'))
$restart.TargetPath = $executable
$restart.Arguments = '--restart'
$restart.WorkingDirectory = $target
$restart.Description = 'Uruchom ponownie również zawieszony widżet'
$restart.Save()
$run = 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Run'
New-Item -Path $run -Force | Out-Null
Set-ItemProperty -Path $run -Name 'ZegarHTC' -Value ('"' + $executable + '"')
Write-Output "Zainstalowano: $executable. Autostart jest włączony."
if (-not $NoLaunch) { Start-Process -FilePath $executable -WorkingDirectory $target }
