; ═══════════════════════════════════════════════════════
;  LanTransfer v1.2.0 — Inno Setup 安装脚本
;  编译工具：Inno Setup 6 (https://jrsoftware.org/isinfo.php)
; ═══════════════════════════════════════════════════════

#define MyAppName "LanTransfer"
#define MyAppVersion "1.2.0"
#define MyAppPublisher "HR"
#define MyAppURL "https://github.com/Harry1999-sky/Transfer"
#define MyAppExeName "LanTransfer.exe"
#define MyAppSourceDir "..\publish"

[Setup]
; 应用信息
AppId={{B8E5C3A1-7F2D-4E8A-9C1B-3D6F5A2E8B4C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=局域网文件传输工具
VersionInfoCopyright=Copyright © 2024 {#MyAppPublisher}
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

; 安装选项
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
LicenseFile=
OutputDir=..\output
OutputBaseFilename=LanTransfer_Setup_v{#MyAppVersion}
SetupIconFile=..\Resources\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4
WizardStyle=modern

; 系统要求
MinVersion=10.0
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; 外观
DisableWelcomePage=no
DisableProgramGroupPage=yes
WizardSizePercent=100
WizardImageFile=
WizardSmallImageFile=

; 其他
UninstallDisplayName={#MyAppName} {#MyAppVersion}
CloseApplications=yes
RestartApplications=no
AppMutex=LanTransferInstance

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenuicon"; Description: "创建开始菜单快捷方式"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce
Name: "associatefiles"; Description: "关联 .lantransfer 文件"; GroupDescription: "其他选项:"; Flags: unchecked

[Files]
; 主程序（单文件自包含）
Source: "{#MyAppSourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; 开始菜单
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"

; 桌面快捷方式
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

; 开始菜单快捷方式（可选）
Name: "{userstartmenu}\Programs\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: startmenuicon

[Run]
; 安装完成后运行
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
; 文件关联（可选）
Root: HKA; Subkey: "Software\Classes\.lantransfer"; ValueType: string; ValueName: ""; ValueData: "LanTransferFile"; Flags: uninsdeletevalue; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\LanTransferFile"; ValueType: string; ValueName: ""; ValueData: "LanTransfer 配置文件"; Flags: uninsdeletekey; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\LanTransferFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: associatefiles
Root: HKA; Subkey: "Software\Classes\LanTransferFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associatefiles

; 应用信息注册
Root: HKA; Subkey: "Software\{#MyAppName}"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\{#MyAppName}"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletekey

[UninstallRun]
; 卸载前关闭进程
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#MyAppExeName}"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
; 卸载时删除应用数据（可选）
Type: filesandordirs; Name: "{app}"

[Code]
// ═══════════════════════════════════════════════════════
//  自定义安装逻辑
// ═══════════════════════════════════════════════════════

// 检查是否已安装旧版本
function GetUninstallString(): String;
var
  sUnInstPath: String;
  sUnInstallString: String;
begin
  sUnInstPath := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1');
  sUnInstallString := '';
  if not RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString) then
    RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;

function IsUpgrade(): Boolean;
begin
  Result := (GetUninstallString() <> '');
end;

// 安装前检查并卸载旧版本
function InitializeSetup(): Boolean;
var
  V: Integer;
  iResultCode: Integer;
  sUnInstallString: String;
begin
  Result := True;
  if IsUpgrade() then
  begin
    V := MsgBox(ExpandConstant('{#MyAppName} 已安装。是否先卸载旧版本？'), mbConfirmation, MB_YESNO);
    if V = IDYES then
    begin
      sUnInstallString := GetUninstallString();
      sUnInstallString := RemoveQuotes(sUnInstallString);
      Exec(sUnInstallString, '/SILENT', '', SW_SHOW, ewWaitUntilTerminated, iResultCode);
      Result := True;
    end
    else
    begin
      Result := False;
    end;
  end;
end;

// 安装完成页面自定义
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // 写入防火墙规则提示
    // 实际规则由应用启动时自动添加
  end;
end;
