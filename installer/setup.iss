; Thin self-extracting wrapper: unpacks the WinUI installer (DorfiksSetup.exe + payload) to a temp
; folder and runs it. All the real UI is the WinUI app.
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
[Setup]
AppId={{6B0F7C0E-6A4B-4F0B-9C55-2B7D1D7E0A11}
AppName=Dorfiks VM GUI
AppVersion={#AppVersion}
AppPublisher=Dorfik
CreateAppDir=no
Uninstallable=no
OutputDir=..\dist
OutputBaseFilename=DorfiksQemuGui-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
LZMAUseSeparateProcess=yes
ArchitecturesAllowed=x64compatible
PrivilegesRequired=lowest
WizardStyle=modern dynamic
DisableWelcomePage=yes
DisableDirPage=yes
DisableProgramGroupPage=yes
DisableReadyPage=yes
DisableFinishedPage=yes
DisableStartupPrompt=yes

[Files]
Source: "..\setup_publish\*"; DestDir: "{tmp}\dsetup"; Flags: recursesubdirs ignoreversion deleteafterinstall

[Run]
Filename: "{tmp}\dsetup\DorfiksSetup.exe"; Flags: waituntilterminated
