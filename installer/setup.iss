#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
[Setup]
AppId={{6B0F7C0E-6A4B-4F0B-9C55-2B7D1D7E0A11}
AppName=Dorfik's GUI for QEMU
AppVersion={#AppVersion}
AppPublisher=Dorfik
DefaultDirName={autopf}\Dorfiks GUI for QEMU
DefaultGroupName=Dorfik's GUI for QEMU
OutputDir=..\dist
OutputBaseFilename=DorfiksQemuGui-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
WizardStyle=modern dynamic
UninstallDisplayIcon={app}\DorfiksQemuGui.exe

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\Dorfik's GUI for QEMU"; Filename: "{app}\DorfiksQemuGui.exe"
Name: "{autodesktop}\Dorfik's GUI for QEMU"; Filename: "{app}\DorfiksQemuGui.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"

[Run]
Filename: "{app}\DorfiksQemuGui.exe"; Description: "Launch Dorfik's GUI for QEMU"; Flags: nowait postinstall skipifsilent
