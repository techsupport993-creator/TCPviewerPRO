@echo off
setlocal
title TCP Viewer Pro - EXE Builder
color 0A
echo.
echo  ======================================
echo       TCP VIEWER PRO - CAT ICON + AUTO UPDATE BUILDER
echo  ======================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [!] .NET 10 SDK was not found.
    echo Install the .NET 10 SDK from:
    echo https://dotnet.microsoft.com/en-us/download/dotnet/10.0
    start "" "https://dotnet.microsoft.com/en-us/download/dotnet/10.0"
    pause
    exit /b 1
)

set "PROJECT=%~dp0TCPViewerPro\TCPViewerPro.csproj"
set "CONFIG=%~dp0NuGet.Config"
set "OUT=%~dp0TCPViewerPro\bin\Release\net10.0-windows\win-x64\publish\TCPViewerPro.exe"

if not exist "%PROJECT%" (
    echo [ERROR] Could not find the project:
    echo %PROJECT%
    echo Make sure you extracted the entire ZIP and did not move this BAT file.
    pause
    exit /b 1
)

echo [1/3] Checking .NET SDK...
dotnet --list-sdks
echo.
echo [2/3] Restoring required .NET packages from the official NuGet feed...
echo If this step fails, check your internet connection or proxy/firewall.
dotnet restore "%PROJECT%" --configfile "%CONFIG%" -r win-x64
if errorlevel 1 (
    echo.
    echo [ERROR] NuGet restore failed.
    echo This is usually a NuGet feed, internet, proxy, or certificate issue.
    echo No application code has been compiled yet.
    pause
    exit /b 1
)

echo.
echo [3/3] Publishing the standalone Windows x64 EXE...
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true --no-restore /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 (
    echo.
    echo [ERROR] Publish failed. Review the build errors above.
    pause
    exit /b 1
)

if not exist "%OUT%" (
    echo [ERROR] Build finished but TCPViewerPro.exe was not found.
    pause
    exit /b 1
)

copy /y "%OUT%" "%~dp0TCPViewerPro.exe" >nul
if errorlevel 1 (
    echo [ERROR] Could not copy the EXE to this folder.
    pause
    exit /b 1
)

echo.
echo  [SUCCESS] TCPViewerPro.exe is ready!
echo  Location: %~dp0TCPViewerPro.exe
echo.
choice /c YN /m "Launch TCP Viewer Pro now"
if errorlevel 2 goto done
start "" "%~dp0TCPViewerPro.exe"
:done
pause
