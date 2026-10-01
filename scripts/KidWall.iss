; KidWall Windows installer.
; Build from the repository root with Inno Setup 6 and pass /DAppVersion=x.y.z.
; /DAppArch selects the target architecture: x64 (default) or x86.

#ifndef AppVersion
#define AppVersion "0.0.0"
#endif

#ifndef AppArch
#define AppArch "x64"
#endif

#if AppArch != "x64" && AppArch != "x86"
#error AppArch must be x64 or x86
#endif

#ifndef SourceDir
#define SourceDir "..\artifacts\publish\win-" + AppArch + "\KidWall.App"
#endif

#ifndef OutputDir
#define OutputDir "..\artifacts\release"
#endif

[Setup]
AppId={{E267CDA1-B2DE-453E-81A4-61F53F8F1040}
AppName=KidWall
AppVersion={#AppVersion}
AppPublisher=Dotnet9
AppPublisherURL=https://github.com/dotnet9/KidWall
AppSupportURL=https://github.com/dotnet9/KidWall/issues
DefaultDirName={autopf}\KidWall
DefaultGroupName=KidWall
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=KidWall-v{#AppVersion}-win-{#AppArch}-setup
Compression=lzma2/ultra64
SolidCompression=yes
#if AppArch == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
PrivilegesRequired=admin
ChangesAssociations=no
CloseApplications=yes
RestartApplications=yes
CloseApplicationsFilter=KidWall.App.exe
UninstallDisplayIcon={app}\KidWall.App.exe
WizardStyle=modern

[Languages]
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\KidWall"; Filename: "{app}\KidWall.App.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\KidWall"; Filename: "{app}\KidWall.App.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\KidWall.App.exe"; Description: "Launch KidWall"; Flags: nowait postinstall skipifsilent
