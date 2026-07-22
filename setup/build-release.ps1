# Puantaj V1.0.2 - Windows release build script
# Bu betik YALNIZCA gerçek bir Windows makinesinde çalıştırılmalıdır (.NET 8 SDK + Inno Setup 6 kurulu olmalı).
# macOS/Linux üzerinde WinForms (net8.0-windows) hedefi derlenemediği için burada çalıştırılamaz.
#
# Kullanım (Windows PowerShell, repo kökünden):
#   .\setup\build-release.ps1
#
# Üretilenler:
#   src\PuantajApp\bin\Release\net8.0-windows\win-x64\publish\PuantajApp.exe
#   src\PuantajLicenseGenerator\bin\Release\net8.0-windows\win-x64\publish\PuantajLicenseGenerator.exe
#   dist\Puantaj_Setup_v1.0.2.exe

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    Write-Host "PuantajApp yayınlanıyor (self-contained, win-x64, Release)..."
    dotnet publish "src\PuantajApp\PuantajApp.csproj" -c Release

    Write-Host "PuantajLicenseGenerator yayınlanıyor (self-contained, win-x64, Release)..."
    dotnet publish "src\PuantajLicenseGenerator\PuantajLicenseGenerator.csproj" -c Release

    $iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($null -eq $iscc) {
        $candidate = Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"
        if (Test-Path $candidate) { $iscc = $candidate } else {
            throw "ISCC.exe bulunamadı. Inno Setup 6'yı kurun: https://jrsoftware.org/isinfo.php"
        }
    }

    Write-Host "Setup.exe oluşturuluyor (Inno Setup)..."
    & $iscc "setup\Puantaj.iss"

    Write-Host ""
    Write-Host "Tamamlandı:"
    Write-Host "  LicenseGenerator: src\PuantajLicenseGenerator\bin\Release\net8.0-windows\win-x64\publish\PuantajLicenseGenerator.exe"
    Write-Host "  Setup:            dist\Puantaj_Setup_v1.0.2.exe"
}
finally {
    Pop-Location
}
