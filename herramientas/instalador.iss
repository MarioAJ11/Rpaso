; ─────────────────────────────────────────────────────────────────────────────
;  Instalador de Repaso (Inno Setup 6)
;
;  Se compila con:  ISCC herramientas\instalador.iss
;  o con el atajo:  .\herramientas\instalar.ps1
;
;  La app se publica antes en .\publicacion (dotnet publish, ver instalar.ps1).
; ─────────────────────────────────────────────────────────────────────────────

#define MiApp        "Repaso"
#define MiVersion    "1.0.0"
#define MiExe        "Rpaso.exe"
#define MiPublisher  "Repaso"

[Setup]
; AppId fijo: las reinstalaciones actualizan la app en lugar de duplicarla.
AppId={{5B9D1E44-7C3A-4F2E-9B6D-0A1C8F3D5E72}
AppName={#MiApp}
AppVersion={#MiVersion}
AppVerName={#MiApp} {#MiVersion}
AppPublisher={#MiPublisher}
VersionInfoVersion={#MiVersion}
VersionInfoDescription={#MiApp}
VersionInfoProductName={#MiApp}
VersionInfoProductVersion={#MiVersion}
DefaultDirName={autopf}\{#MiApp}
DefaultGroupName={#MiApp}
OutputBaseFilename=Rpaso-{#MiVersion}-setup
OutputDir=..\dist
SetupIconFile=..\Assets\repaso.ico
UninstallDisplayIcon={app}\{#MiExe}
UninstallDisplayName={#MiApp} {#MiVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; La app es solo de Windows 10/11 de 64 bits (el OCR de Windows lo exige).
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
SetupLogging=yes
LicenseFile=..\LICENSE.txt
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el &Escritorio"; GroupDescription: "Accesos directos:"
Name: "autostart"; Description: "Iniciar {#MiApp} al encender el ordenador"; GroupDescription: "Opciones:"; Flags: unchecked

[Files]
; Todo lo publicado (app autocontenida: 236 MB con el runtime de .NET dentro).
Source: "..\publicacion\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; El certificado de firma, para añadirlo a la confianza del equipo.
Source: "..\herramientas\certificado\Rpaso-Dev-Signing.cer"; DestDir: "{tmp}"; \
  Flags: deleteafterinstall

[Run]
; Code Integrity (Smart App Control) valida la firma con los almacenes de la MÁQUINA.
; Un certificado que solo está en el almacén del usuario no vale: la app arranca pero
; al cargar sus DLL se bloquea con 0x800711C7. Por eso el instalador (que corre
; elevado) lo añade a "Raíces de confianza" y "Editores de confianza" del equipo.
Filename: "{sys}\certutil.exe"; Parameters: "-addstore -f Root ""{tmp}\Rpaso-Dev-Signing.cer"""; \
  StatusMsg: "Configurando la confianza del certificado..."; Flags: runhidden waituntilterminated
Filename: "{sys}\certutil.exe"; Parameters: "-addstore -f TrustedPublisher ""{tmp}\Rpaso-Dev-Signing.cer"""; \
  StatusMsg: "Configurando la confianza del certificado..."; Flags: runhidden waituntilterminated
Filename: "{app}\{#MiExe}"; Description: "Iniciar {#MiApp} ahora"; WorkingDir: "{app}"; \
  Flags: nowait postinstall skipifsilent

[Icons]
Name: "{group}\{#MiApp}"; Filename: "{app}\{#MiExe}"; WorkingDir: "{app}"
Name: "{group}\Desinstalar {#MiApp}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MiApp}"; Filename: "{app}\{#MiExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
; Inicio automático (opcional). Va en HKLM porque la instalación es para todos los usuarios.
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
  ValueName: "{#MiApp}"; ValueData: """{app}\{#MiExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[UninstallDelete]
; Los datos (exámenes, carpetas y progreso) viven en %APPDATA%\{#MiApp} y NO se tocan aquí.
Type: filesandordirs; Name: "{app}"

[Code]
{ Al desinstalar se ofrece borrar los datos del usuario. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Datos: String;
begin
  if (CurUninstallStep = usPostUninstall) and (not UninstallSilent) then
  begin
    Datos := ExpandConstant('{userappdata}\{#MiApp}');
    if DirExists(Datos) then
    begin
      if MsgBox('¿Eliminar también tus exámenes, carpetas y el progreso guardados?' + #13#10#13#10 +
                Datos + #13#10#13#10 +
                'Si no los eliminas, se conservarán para la próxima instalación.',
                mbConfirmation, MB_YESNO) = IDYES then
        DelTree(Datos, True, True, True);
    end;
  end;
end;
