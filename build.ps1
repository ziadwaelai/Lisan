# Builds Lisan.exe using the C# compiler that ships with Windows (.NET Framework 4.8). No installs needed.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$fw   = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$csc  = Join-Path $fw 'csc.exe'
$out  = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $out | Out-Null

# 1) Make the app icon from the same drawing code the app uses.
$ico = Join-Path $out 'Lisan.ico'
if (-not ('Lisan.IconArt' -as [type])) {
    Add-Type -Path (Join-Path $root 'src\IconArt.cs'), (Join-Path $root 'src\Native.cs') -ReferencedAssemblies System.Drawing
}
[Lisan.IconArt]::SaveIco($ico)

# 2) Compile.
$sources = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
& $csc /nologo /codepage:65001 /target:winexe /optimize+ /platform:anycpu `
    /out:"$out\Lisan.exe" /win32icon:"$ico" `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /reference:"$fw\System.Web.Extensions.dll" `
    $sources
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "Built $out\Lisan.exe"
