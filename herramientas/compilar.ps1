<#
.SYNOPSIS
    Compila la aplicación y firma los binarios, para que Smart App Control la deje ejecutar.

.DESCRIPTION
    Atajo de los dos pasos que hacen falta en Windows 11 con la política
    "VerifiedAndReputableDesktopFlightSupplemental" de Microsoft activa:

        dotnet build
        .\herramientas\firmar.ps1

    Sin el paso de firma, Windows bloquea el ejecutable con
    "Una directiva de Control de aplicaciones bloqueó este archivo. (0x800711C7)".

.EXAMPLE
    .\herramientas\compilar.ps1
    .\herramientas\compilar.ps1 -Configuracion Release
#>
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuracion = "Debug"
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot

Push-Location $raiz

try {
    Write-Host "==> Compilando ($Configuracion)..." -ForegroundColor Cyan
    & dotnet build --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "La compilación falló." }

    # La carpeta de salida incluye el TFM, que puede cambiar (net10.0, net10.0-windows...).
    $tfm = (Select-String -Path "Rpaso.csproj" -Pattern "<TargetFramework>(?<t>[^<]+)</TargetFramework>").Matches[0].Groups["t"].Value
    $salida = ".\bin\$Configuracion\$tfm"

    Write-Host "==> Firmando ($salida)..." -ForegroundColor Cyan
    & powershell.exe -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "firmar.ps1") -Ruta $salida

    Write-Host "`n==> Listo. Ejecuta: dotnet run" -ForegroundColor Green
}
finally {
    Pop-Location
}
