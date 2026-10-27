@echo off
echo ============================================================
echo Launching Unity Project with DirectX 11 (Bypasses D3D12 Crash)
echo ============================================================
echo.
taskkill /F /IM UnityBugReporter.exe 2>nul
if exist "Temp\UnityLockfile" (
    del /F /Q "Temp\UnityLockfile" 2>nul
)
start "" "C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe" -projectPath "%~dp0." -force-d3d11
echo Unity is starting in DirectX 11 mode...
