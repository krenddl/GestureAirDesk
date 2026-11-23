@echo off
cd /d "%~dp0"
if exist "%~dp0release\GestureFlow.AirDesk-TelegramPhotoTimer\GestureFlow.Desktop.exe" (
    start "" "%~dp0release\GestureFlow.AirDesk-TelegramPhotoTimer\GestureFlow.Desktop.exe"
    exit /b
)
dotnet run --project "%~dp0src\GestureFlow.Desktop\GestureFlow.Desktop.csproj" -c Release
if errorlevel 1 pause
