<#
.SYNOPSIS
    Publica la aplicación, la firma y genera el instalador .exe (Inno Setup).

.DESCRIPTION
    Encadena los pasos que hacen falta para tener un instalador entregable:

        1) dotnet publish  ->  .\publicacion\   (app autocontenida, 236 MB)
        2) firmar.ps1      ->  firma los binarios para el equipo de desarrollo
        3) ISCC            ->  .\dist\Rpaso-<version>-setup.exe
        4) firmar.ps1      ->  firma el instalador

    El instalador se firma con el mismo certificado que la app. Un certificado
    autofirmado sirve para el equipo donde se creó, pero Smart App Control solo
    acepta certificados emitidos por una CA pública: para entregar la app a otra
    persona sin bloqueos hace falta un certificado de firma de código comercial
    (ver README). El instalador NO instala el certificado de desarrollo.

.PARAMETER SinPublicar
    No vuelve a publicar: reutiliza la carpeta .\publicacion que ya existe.

.PARAMETER Configuracion
    Debug o Release. Por defecto Release.

.EXAMPLE
    .\herramientas\instalar.ps1
    .\herramientas\instalar.ps1 -SinPublicar
#>
[CmdletBinding()]
param(
    [switch]$SinPublicar,
    [ValidateSet("Debug", "Release")]
    [string]$Configuracion = "Release"
)

$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent $PSScriptRoot

Push-Location $raiz

try {
    # --- 1) Publicar ------------------------------------------------------------
    if (-not $SinPublicar) {
        Write-Host "==> Publicando ($Configuracion, win-x64, autocontenido)..." -ForegroundColor Cyan

        if (Test-Path ".\publicacion") {
            Remove-Item ".\publicacion" -Recurse -Force
        }

        & dotnet publish -c $Configuracion -r win-x64 --self-contained true -o publicacion "-p:UsedAvaloniaProducts="
        if ($LASTEXITCODE -ne 0) { throw "La publicación falló." }
    }
    else {
        Write-Host "==> Se reutiliza la publicación existente." -ForegroundColor DarkGray
    }

    if (-not (Test-Path ".\publicacion\Rpaso.exe")) {
        throw "No existe .\publicacion\Rpaso.exe: publica antes de compilar el instalador."
    }

    # --- 2) Firmar la app ------------------------------------------------------
    Write-Host "`n==> Firmando la aplicación..." -ForegroundColor Cyan

    & powershell.exe -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "firmar.ps1") `
        -Ruta ".\publicacion"
    if ($LASTEXITCODE -ne 0) { throw "El firmado de la aplicación falló." }

    # --- 3) Localizar el compilador de Inno Setup ------------------------------
    $iscc = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $iscc) {
        $enPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        if ($enPath) { $iscc = $enPath.Source }
    }

    if (-not $iscc) {
        throw "Inno Setup no está instalado. Instálalo con: winget install --id JRSoftware.InnoSetup -e"
    }

    # --- 4) Compilar el instalador ---------------------------------------------
    Write-Host "`n==> Compilando el instalador..." -ForegroundColor Cyan

    if (Test-Path ".\dist") {
        Remove-Item ".\dist" -Recurse -Force
    }

    & $iscc ".\herramientas\instalador.iss"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup no pudo compilar el instalador." }

    $instalador = Get-ChildItem ".\dist" -Filter "*.exe" | Select-Object -First 1
    if (-not $instalador) { throw "No se generó el instalador." }

    # --- 5) Firmar el instalador ----------------------------------------------
    Write-Host "`n==> Firmando el instalador..." -ForegroundColor Cyan
    & powershell.exe -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "firmar.ps1") -Fichero $instalador.FullName
    if ($LASTEXITCODE -ne 0) { throw "El firmado del instalador falló." }

    $firma = Get-AuthenticodeSignature $instalador.FullName
    $mb = [math]::Round($instalador.Length / 1MB, 1)

    Write-Host "`n==> Listo." -ForegroundColor Green
    Write-Host ("    Instalador : {0} ({1} MB)" -f $instalador.FullName, $mb) -ForegroundColor Green
    Write-Host ("    Firma      : {0}" -f $firma.Status) -ForegroundColor Green
    Write-Host "`n    El instalador no instala el certificado de desarrollo en el equipo destino." -ForegroundColor DarkGray
    Write-Host "    Si Smart App Control está activado en otro equipo, Windows puede bloquear" -ForegroundColor Yellow
    Write-Host "    la app: solo acepta certificados de una CA pública. Para una entrega sin" -ForegroundColor Yellow
    Write-Host "    bloqueos, usa un certificado de firma de código comercial (ver README)." -ForegroundColor Yellow
}
finally {
    Pop-Location
}
