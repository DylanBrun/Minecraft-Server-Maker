; Installeur Windows de Minecraft Server Maker (Inno Setup 6).
; Se construit avec installer\build.ps1, qui publie l'exe puis compile ce script avec la version du .csproj.

#define AppName "Minecraft Server Maker"
#define AppExe "MinecraftServerMaker.exe"
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
; AppId identifie l'app pour les mises à jour et la désinstallation : ne jamais le changer.
AppId={{59F6346E-3849-440C-81FF-7B179AA02798}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Dylan
; Installation pour l'utilisateur courant, sans demande d'autorisation administrateur.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
OutputDir=..\publish
OutputBaseFilename=MinecraftServerMaker-Setup-{#AppVersion}
SetupIconFile=..\src\MinecraftServerMaker\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern
WizardImageFile=wizard.bmp,wizard-2x.bmp
WizardSmallImageFile=wizard-small.bmp,wizard-small-2x.bmp
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0
; Ferme l'app si elle tourne pendant une mise à jour ou une désinstallation.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Messages]
french.WelcomeLabel2=Cet assistant va installer [name/ver] sur votre ordinateur.%n%nChoisissez un modpack CurseForge, cliquez sur « Créer le serveur », puis sur le gros bouton vert : votre serveur est prêt.
french.ConfirmUninstall=Voulez-vous désinstaller %1 ?%n%nVos serveurs, leurs mondes et vos réglages (dossier %%AppData%%\MCServerTray) seront conservés.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
