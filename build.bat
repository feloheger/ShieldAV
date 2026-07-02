@echo off
setlocal EnableDelayedExpansion
title ShieldAV – Build Script
color 0A

echo.
echo  ===================================================
echo   ShieldAV Antivirus  –  Build Script
echo   .NET 8 Self-Contained + Inno Setup 6
echo  ===================================================
echo.

:: Check dotnet
where dotnet >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo [FEHLER] .NET 8 SDK nicht gefunden!
    echo Download: https://dotnet.microsoft.com/download/dotnet/8.0
    echo Hinweis: Nur das SDK wird zum BAUEN benoetigt.
    echo          Benutzer brauchen kein .NET installieren.
    pause & exit /b 1
)

:: Check Inno Setup
set INNO=C:\Program Files (x86)\Inno Setup 6\ISCC.exe
if not exist "%INNO%" (
    echo [WARNUNG] Inno Setup 6 nicht gefunden unter:
    echo           %INNO%
    echo           Nur die EXE wird gebaut, kein Installer.
    set HAS_INNO=0
) else (
    set HAS_INNO=1
)

echo [1/4] NuGet Pakete wiederherstellen...
dotnet restore ShieldAV\ShieldAV.csproj --verbosity quiet
if %ERRORLEVEL% neq 0 ( echo [FEHLER] Restore fehlgeschlagen! & pause & exit /b 1 )

echo [2/4] Build (Release)...
dotnet build ShieldAV\ShieldAV.csproj -c Release --no-restore --verbosity quiet
if %ERRORLEVEL% neq 0 ( echo [FEHLER] Build fehlgeschlagen! & pause & exit /b 1 )

echo [3/4] Publish (self-contained, win-x64, kein .NET noetig)...
dotnet publish ShieldAV\ShieldAV.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishReadyToRun=true ^
    -p:PublishTrimmed=false ^
    -p:PublishSingleFile=false ^
    -o publish ^
    --no-restore ^
    --verbosity quiet

if %ERRORLEVEL% neq 0 ( echo [FEHLER] Publish fehlgeschlagen! & pause & exit /b 1 )

echo.
echo  Publish-Groesse:
dir /s publish\*.* | find "Datei(en)"

echo.
echo [4/4] Installer erstellen...
if "%HAS_INNO%"=="1" (
    if not exist LICENSE.txt echo ShieldAV Antivirus - Freeware > LICENSE.txt
    if not exist README.md   echo # ShieldAV Antivirus > README.md
    mkdir installer_output 2>nul

    "%INNO%" installer.iss
    if %ERRORLEVEL% neq 0 (
        echo [FEHLER] Inno Setup Kompilierung fehlgeschlagen!
        pause & exit /b 1
    )
    echo.
    echo  ===================================================
    echo   BUILD ERFOLGREICH!
    echo  ===================================================
    echo   EXE-Ordner : publish\
    echo   Installer  : installer_output\ShieldAV_Setup_v1.0.0.exe
    echo.
    echo   Der Installer benoetigt kein .NET auf dem Ziel-PC!
    echo  ===================================================
) else (
    echo  [UEBERSPRUNGEN] Inno Setup nicht vorhanden.
    echo.
    echo  publish\ShieldAV.exe ist self-contained und
    echo  benoetigt kein .NET auf dem Ziel-PC.
)

echo.
pause
