; Instalador do KidLock (Inno Setup 6). Gere com:  .\build.ps1 -Target Installer
;
; Leva junto tudo o que o programa precisa (inclusive o runtime .NET): não há pré-requisitos.
; Instala só para o usuário atual, sem pedir administrador.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef NumericVersion
  #define NumericVersion AppVersion
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\installer-win-x86"
#endif
#ifndef Publisher
  #define Publisher "MetaBee"
#endif

[Setup]
AppId={{2FA7F348-C744-4AF4-AE20-C9A9AAAFF6CC}
AppName=KidLock
AppVersion={#AppVersion}
AppVerName=KidLock {#AppVersion}
AppPublisher={#Publisher}
#ifdef RepoUrl
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
#endif
VersionInfoVersion={#NumericVersion}
VersionInfoProductName=KidLock
VersionInfoDescription=Instalador do KidLock
DefaultDirName={autopf}\KidLock
DisableDirPage=yes
DefaultGroupName=KidLock
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0.14393
OutputDir=..\artifacts\installer
OutputBaseFilename=KidLock-Setup-{#AppVersion}
SetupIconFile=..\src\KidLock.App\Assets\kidlock.ico
UninstallDisplayIcon={app}\KidLock.exe
UninstallDisplayName=KidLock
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; O próprio [Code] encerra o KidLock antes de copiar arquivos (ver StopKidLock).
CloseApplications=no
InfoAfterFile=ComoUsar.txt
ShowLanguageDialog=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "autostart"; Description: "Iniciar o KidLock junto com o Windows (recomendado)"
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; Flags: unchecked

[InstallDelete]
; Bibliotecas de versões anteriores e o atalho criado pelo antigo script de instalação manual.
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{userprograms}\KidLock.lnk"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\KidLock"; Filename: "{app}\KidLock.exe"; Comment: "Bloqueia e desbloqueia teclado e touchpad"
Name: "{group}\Desinstalar o KidLock"; Filename: "{uninstallexe}"
Name: "{userdesktop}\KidLock"; Filename: "{app}\KidLock.exe"; Tasks: desktopicon

[Registry]
; Mesmo valor usado pelo menu "Iniciar com o Windows" do programa, então os dois ficam sincronizados.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "KidLock"; ValueData: """{app}\KidLock.exe"""; Tasks: autostart
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "KidLock"; Flags: deletevalue; Tasks: not autostart
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "KidLock"; Flags: uninsdeletevalue

[UninstallDelete]
; A pasta pode ter sido criada por uma instalação manual antiga; o Inno só remove o que ele criou.
Type: dirifempty; Name: "{app}"

[Run]
Filename: "{app}\KidLock.exe"; Description: "Abrir o KidLock agora"; Flags: nowait postinstall skipifsilent

[Code]
procedure StopKidLock();
var
  ResultCode: Integer;
begin
  { Encerrar o processo remove os ganchos de entrada: teclado e touchpad voltam na hora. }
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM KidLock.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(500);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopKidLock();
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    StopKidLock();

  if (CurUninstallStep = usPostUninstall) and not UninstallSilent then
    if MsgBox('Remover também as configurações e os registros (logs) do KidLock?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
    begin
      DelTree(ExpandConstant('{userappdata}\KidLock'), True, True, True);
      DelTree(ExpandConstant('{localappdata}\KidLock'), True, True, True);
    end;
end;
