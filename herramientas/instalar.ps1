<#
.SYNOPSIS
    Publica la aplicación, la firma y genera el instalador .exe (Inno Setup).

.DESCRIPTION
    Encadena los tres pasos que hacen falta para tener un instalador entregable:

        1) dotnet publish  ->  .\publicacion\   (app autocontenida, 236 MB)
        2) firmar.ps1      ->  firma los binarios (Smart App Control lo exige)
        3) ISCC            ->  .\dist\Rpaso-<version>-setup.exe
        4) firmar.ps1      ->  firma el instalador (también lo evalúa Smart App Control)

    El instalador se firma con el mismo certificado que la app. Con un
    certificado autofirmado solo arranca en el equipo donde se creó; para
    distribución al público hace falta un certificado de firma de código
    comercial (ver README).

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

        & dotnet publish -c $Configuracion -r win-x64 --self-contained true -o publicacion
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
    $cer = Join-Path $PSScriptRoot "certificado\Rpaso-Dev-Signing.cer"

    & powershell.exe -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "firmar.ps1") `
        -Ruta ".\publicacion" -ExportarCertificado $cer
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
    Write-Host "`n    El instalador añade el certificado de firma a la confianza del equipo," -ForegroundColor DarkGray
    Write-Host "    sin él Smart App Control bloquea la carga de las DLL (0x800711C7)." -ForegroundColor DarkGray
    Write-Host "    En un equipo ajeno, esto instala una CA personal en el equipo: para" -ForegroundColor Yellow
    Write-Host "    distribución al público, usa un certificado de firma de código comercial." -ForegroundColor Yellow
}
finally {
    Pop-Location
}
