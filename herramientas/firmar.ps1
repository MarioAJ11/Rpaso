<#
.SYNOPSIS
    Firma los binarios de la aplicación para que Windows Defender Application Control
    (Smart App Control / ISG) permita ejecutarlos.

.DESCRIPTION
    La política "VerifiedAndReputableDesktopFlightSupplemental" que Microsoft distribuye
    con Windows 11 exige que el ejecutable esté firmado o tenga reputación en la nube.
    Un binario recién compilado no la tiene, y Smart App Control lo bloquea con:

        Una directiva de Control de aplicaciones bloqueó este archivo. (0x800711C7)

    Firma con un certificado de firma de código (autofirmado sirve para desarrollo)
    y el bloqueo desaparece.

    En un entorno de empresa, el certificado debe emitirlo la CA corporativa.
    Para distribución al público hace falta un certificado de firma de código comercial.

.PARAMETER Ruta
    Carpeta con los binarios a firmar. Por defecto, la salida de compilación.

.EXAMPLE
    .\herramientas\firmar.ps1
    .\herramientas\firmar.ps1 -Ruta .\publicacion
#>
[CmdletBinding()]
param(
    [string]$Ruta = ".\bin\Debug\net10.0",
    [string]$NombreCertificado = "Rpaso Dev Signing"
)

$ErrorActionPreference = "Stop"

# --- Certificado: se crea uno autofirmado la primera vez -----------------------
$cert = Get-ChildItem Cert:\CurrentUser\My |
    Where-Object { $_.Subject -like "*$NombreCertificado*" } |
    Select-Object -First 1

if (-not $cert) {
    Write-Host "Creando certificado de firma '$NombreCertificado'..." -ForegroundColor Cyan
    $cert = New-SelfSignedCertificate `
        -Subject "CN=$NombreCertificado" `
        -Type CodeSigningCert `
        -CertStoreLocation "Cert:\CurrentUser\My" `
        -KeyUsage DigitalSignature `
        -NotAfter (Get-Date).AddYears(2)

    # La raíz debe estar en el almacén de confianza para que la cadena valide.
    # certutil no pide confirmación, a diferencia de Import-Certificate.
    $cer = Join-Path $env:TEMP "rpaso-firma.cer"
    Export-Certificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $cer -Force | Out-Null
    & certutil.exe -addstore -user Root $cer | Out-Null

    Write-Host "  Thumbprint: $($cert.Thumbprint)" -ForegroundColor DarkGray
}

# La raíz se añade siempre (no solo al crear): si el certificado ya existía pero no estaba
# en el almacén de confianza, la cadena no validaba.
$cer = Join-Path $env:TEMP "rpaso-firma.cer"
Export-Certificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" -FilePath $cer -Force | Out-Null
& certutil.exe -addstore -user Root $cer | Out-Null

if (-not (Test-Path $Ruta)) {
    Write-Error "No existe la carpeta '$Ruta'."
    exit 1
}

# --- Firmado de los PE de la salida -------------------------------------------
# Los ensamblados de terceros (Avalonia, PdfPig...) ya vienen firmados y no hay que tocarlos.
$ficheros = Get-ChildItem -Path $Ruta -Filter "Rpaso.*" -File |
    Where-Object { $_.Extension -in ".exe", ".dll" }

if (-not $ficheros) {
    Write-Host "No hay binarios que firmar en '$Ruta'." -ForegroundColor Yellow
    exit 0
}

Write-Host "Firmando $($ficheros.Count) fichero(s) en '$Ruta'..." -ForegroundColor Cyan

# La marca de tiempo (contrafirma de una TSA) NO es opcional: Smart App Control exige que
# la firma esté respaldada por una TSA de confianza. Sin ella, el binario se bloquea igual.
$servidores = @(
    "http://timestamp.digicert.com",
    "http://timestamp.szoned.cz",
    "http://timestamp.globalsign.com/tsa/r6advanced1"
)

foreach ($fichero in $ficheros) {
    $firmado = $false

    foreach ($servidor in $servidores) {
        Set-AuthenticodeSignature `
            -FilePath $fichero.FullName `
            -Certificate $cert `
            -HashAlgorithm SHA256 `
            -TimestampServer $servidor `
            -ErrorAction SilentlyContinue | Out-Null

        $estado = (Get-AuthenticodeSignature $fichero.FullName).Status
        $conMarca = $null -ne (Get-AuthenticodeSignature $fichero.FullName).TimeStamperCertificate

        if ($estado -eq "Valid" -and $conMarca) {
            Write-Host ("  {0,-14} {1} (marcado: {2})" -f $fichero.Name, $estado, $servidor) -ForegroundColor DarkGray
            $firmado = $true
            break
        }
    }

    if (-not $firmado) {
        Write-Host ("  {0,-14} SIN FIRMA VALIDA" -f $fichero.Name) -ForegroundColor Red
    }
}

Write-Host "Listo." -ForegroundColor Green
