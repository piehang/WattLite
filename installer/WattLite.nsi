Unicode true
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"

!define APPNAME "WattLite"
!define APPDISPLAY "WattLite 加速器"
!define APPVERSION "1.0.0"
!define APPEXE "WattLite.exe"
!define REGUNINST "Software\Microsoft\Windows\CurrentVersion\Uninstall\WattLite"

Name "${APPDISPLAY}"
OutFile "WattLite-Setup-${APPVERSION}.exe"
InstallDir "$PROGRAMFILES64\WattLite"
InstallDirRegKey HKLM "Software\WattLite" "InstallDir"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
SetCompressorDictSize 64
BrandingText "WattLite"

VIProductVersion "${APPVERSION}.0"
VIAddVersionKey /LANG=2052 "ProductName" "${APPDISPLAY}"
VIAddVersionKey /LANG=2052 "FileDescription" "WattLite 安装程序"
VIAddVersionKey /LANG=2052 "FileVersion" "${APPVERSION}.0"
VIAddVersionKey /LANG=2052 "ProductVersion" "${APPVERSION}.0"
VIAddVersionKey /LANG=2052 "LegalCopyright" "WattLite"

!define MUI_ICON "app.ico"
!define MUI_UNICON "app.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APPEXE}"
!define MUI_FINISHPAGE_RUN_TEXT "立即启动 WattLite"

!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "SimpChinese"

Section "WattLite 主程序" SecMain
  SetShellVarContext all
  SetRegView 64
  SetOutPath "$INSTDIR"
  File /r "..\publish\*.*"

  CreateDirectory "$SMPROGRAMS\WattLite"
  CreateShortcut "$SMPROGRAMS\WattLite\WattLite.lnk" "$INSTDIR\${APPEXE}" "" "$INSTDIR\${APPEXE}" 0
  CreateShortcut "$SMPROGRAMS\WattLite\卸载 WattLite.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut "$DESKTOP\WattLite.lnk" "$INSTDIR\${APPEXE}" "" "$INSTDIR\${APPEXE}" 0

  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKLM "Software\WattLite" "InstallDir" "$INSTDIR"

  WriteRegStr HKLM "${REGUNINST}" "DisplayName" "${APPDISPLAY}"
  WriteRegStr HKLM "${REGUNINST}" "DisplayIcon" "$INSTDIR\${APPEXE}"
  WriteRegStr HKLM "${REGUNINST}" "DisplayVersion" "${APPVERSION}"
  WriteRegStr HKLM "${REGUNINST}" "Publisher" "WattLite"
  WriteRegStr HKLM "${REGUNINST}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "${REGUNINST}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKLM "${REGUNINST}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegDWORD HKLM "${REGUNINST}" "NoModify" 1
  WriteRegDWORD HKLM "${REGUNINST}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${REGUNINST}" "EstimatedSize" "$0"
SectionEnd

Section "Uninstall"
  SetShellVarContext all
  SetRegView 64
  nsExec::Exec 'taskkill /F /IM WattLite.exe'
  nsExec::Exec 'taskkill /F /IM Steam++.Accelerator.exe'
  Sleep 800

  ; 还原 hosts（清除加速写入的域名映射）
  ExecWait '"$INSTDIR\${APPEXE}" --cleanup'

  Delete "$DESKTOP\WattLite.lnk"
  Delete "$SMPROGRAMS\WattLite\WattLite.lnk"
  Delete "$SMPROGRAMS\WattLite\卸载 WattLite.lnk"
  RMDir "$SMPROGRAMS\WattLite"

  RMDir /r "$INSTDIR"

  DeleteRegKey HKLM "${REGUNINST}"
  DeleteRegKey HKLM "Software\WattLite"
SectionEnd
