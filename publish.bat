@echo off
chcp 65001 >nul
echo ========================================
echo   LanTransfer 单文件绿色版打包
echo ========================================
echo.

echo [1/2] 正在发布 win-x64 单文件版本...
dotnet publish -c Release -o ./publish

if %errorlevel% neq 0 (
    echo.
    echo 发布失败！
    pause
    exit /b 1
)

echo.
echo [2/2] 清理多余文件并重命名...
cd publish
for %%f in (*) do (
    if /i not "%%f"=="LanTransfer.exe" (
        del "%%f" >nul 2>&1
    )
)
for /d %%d in (*) do (
    rd /s /q "%%d" >nul 2>&1
)
ren LanTransfer.exe LanTransfer_v1.2.0.exe
cd ..

echo.
echo ========================================
echo 打包完成！
echo 输出: .\publish\LanTransfer_v1.2.0.exe
echo ========================================
echo.
pause
