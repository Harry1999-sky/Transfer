@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo   LanTransfer v1.2.0 安装包构建
echo ========================================
echo.

:: ─── 配置 ───
set VERSION=1.2.0
set PUBLISH_DIR=publish
set OUTPUT_DIR=output
set INNO_SETUP="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
set INNO_SETUP_ALT="C:\Program Files\Inno Setup 6\ISCC.exe"

:: 检查 Inno Setup
if exist %INNO_SETUP% (
    set ISCC=%INNO_SETUP%
) else if exist %INNO_SETUP_ALT% (
    set ISCC=%INNO_SETUP_ALT%
) else (
    echo [错误] 未找到 Inno Setup 6
    echo 请先安装：https://jrsoftware.org/isinfo.php
    echo 安装后重新运行此脚本
    pause
    exit /b 1
)

:: ─── Step 1: 发布 ───
echo [1/3] 正在发布 win-x64 单文件版本...
dotnet publish -c Release -o %PUBLISH_DIR%
if %errorlevel% neq 0 (
    echo 发布失败！
    pause
    exit /b 1
)

:: 清理多余文件
echo [2/3] 清理多余文件...
cd %PUBLISH_DIR%
for %%f in (*) do (
    if /i not "%%f"=="LanTransfer.exe" (
        del "%%f" >nul 2>&1
    )
)
for /d %%d in (*) do (
    rd /s /q "%%d" >nul 2>&1
)
cd ..

:: ─── Step 2: 编译安装包 ───
echo [3/3] 正在编译安装包...
if not exist %OUTPUT_DIR% mkdir %OUTPUT_DIR%
%ISCC% /Q "Installer\LanTransfer.iss"
if %errorlevel% neq 0 (
    echo 安装包编译失败！
    pause
    exit /b 1
)

echo.
echo ========================================
echo 构建完成！
echo.
echo 绿色版:  %PUBLISH_DIR%\LanTransfer.exe
echo 安装包:  %OUTPUT_DIR%\LanTransfer_Setup_v%VERSION%.exe
echo ========================================
echo.
pause
