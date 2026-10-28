# Builds installer\TS_EMS_Setup_<version>.exe in one go:
#   1. publish the app self-contained into PublishOutput (no .NET install needed on site PCs)
#   2. obfuscate TS_EMS.dll with Obfuscar (obfuscar.xml)
#   3. compile setup.iss with Inno Setup 6
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$out = Join-Path $root 'PublishOutput'
Push-Location $root
try {
    Write-Host '== 1/3 Publishing app ==' -ForegroundColor Cyan
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    dotnet publish Frontend\Keemya.Frontend.csproj -c Release -r win-x64 --self-contained true -o $out
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

    Write-Host '== 2/3 Obfuscating ==' -ForegroundColor Cyan
    # obfuscar.xml points at build_staging (tracked in git); run it against PublishOutput instead.
    $obfConfig = Join-Path $out 'obfuscar.publish.xml'
    (Get-Content (Join-Path $root 'obfuscar.xml') -Raw) -replace '\.\\build_staging', '.\PublishOutput' | Set-Content $obfConfig -Encoding utf8
    obfuscar.console $obfConfig
    if ($LASTEXITCODE -ne 0) { throw 'Obfuscar failed' }
    Copy-Item (Join-Path $out 'Obfuscated\TS_EMS.dll') (Join-Path $out 'TS_EMS.dll') -Force
    Remove-Item (Join-Path $out 'Obfuscated') -Recurse -Force
    Remove-Item $obfConfig -Force

    Write-Host '== 3/3 Building installer ==' -ForegroundColor Cyan
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) { throw 'Inno Setup 6 not found. Install it from https://jrsoftware.org/isdl.php and run this script again.' }
    & $iscc (Join-Path $root 'setup.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compile failed' }

    Write-Host "Done: $(Get-ChildItem (Join-Path $root 'installer') -Filter 'TS_EMS_Setup_*.exe' | Sort-Object LastWriteTime | Select-Object -Last 1 -ExpandProperty FullName)" -ForegroundColor Green
}
finally {
    Pop-Location
}
