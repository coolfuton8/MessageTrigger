# Builds installer\MessageTrigger-Setup-<version>.exe
# Requires the .NET 8 SDK and Inno Setup 6 (winget install JRSoftware.InnoSetup).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Write-Host "Publishing self-contained win-x64 build..."
dotnet publish "$root\MessageTrigger.vbproj" -c Release -r win-x64 --self-contained true -o "$root\publish" -nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$candidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)
$iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup" }

Write-Host "Compiling installer..."
& $iscc "$PSScriptRoot\MessageTrigger.iss"
if ($LASTEXITCODE -ne 0) { throw "ISCC failed" }
Get-ChildItem "$PSScriptRoot\MessageTrigger-Setup-*.exe" | Select-Object Name, @{n='MB';e={[math]::Round($_.Length/1MB,1)}}
