# Removes Lisan for the current user. Pass -KeepSettings:$false to also delete your API key and settings.
param([bool]$KeepSettings = $true)
Get-Process Lisan -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500
Remove-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'Lisan' -ErrorAction SilentlyContinue
Remove-Item (Join-Path ([Environment]::GetFolderPath('Programs')) 'Lisan.lnk') -ErrorAction SilentlyContinue
Remove-Item (Join-Path $env:LOCALAPPDATA 'Programs\Lisan') -Recurse -Force -ErrorAction SilentlyContinue
if (-not $KeepSettings) { Remove-Item (Join-Path $env:APPDATA 'Lisan') -Recurse -Force -ErrorAction SilentlyContinue }
Write-Host "Lisan removed."
