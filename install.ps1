# Installs Lisan for the current user (no admin needed).
# - Copies Lisan.exe to %LOCALAPPDATA%\Programs\Lisan
# - Adds a Start menu shortcut
# - Starts Lisan with Windows
# Your settings (API key, hotkeys) stay in %APPDATA%\Lisan and are kept.
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'dist\Lisan.exe'
if (-not (Test-Path $source)) { throw "dist\Lisan.exe not found. Run build.ps1 first." }

$target = Join-Path $env:LOCALAPPDATA 'Programs\Lisan'
$exe    = Join-Path $target 'Lisan.exe'

# Close the running copy so the file can be replaced.
Get-Process Lisan -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

New-Item -ItemType Directory -Force $target | Out-Null
Copy-Item $source $exe -Force

# Start menu shortcut
$lnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'Lisan.lnk'
$shell = New-Object -ComObject WScript.Shell
$sc = $shell.CreateShortcut($lnk)
$sc.TargetPath = $exe
$sc.WorkingDirectory = $target
$sc.Description = 'Translate and fix text anywhere'
$sc.Save()

# Start with Windows
Set-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'Lisan' -Value "`"$exe`""

Start-Process $exe
Write-Host "Lisan installed to $exe and started. It will start with Windows."
