@echo off
for /f %%a in ('echo prompt $E^| cmd') do set "ESC=%%a"
setlocal

set "publishDir=Wauncher\bin\Release\net8.0-windows7.0\win-x64\publish"

echo =============================
echo %ESC%[42mBuilding Wauncher...%ESC%[0m
dotnet publish Wauncher\Wauncher.csproj -c Release -r win-x64 --self-contained false
if errorlevel 1 (
  echo Publish failed.
  exit /b 1
)

echo =============================
echo %ESC%[41mHashing wauncher.exe...%ESC%[0m
certutil -hashfile "%publishDir%\wauncher.exe" MD5

echo =============================
echo %ESC%[1;43mCopying Wauncher publish output...%ESC%[0m
set "defaultDest=C:\Games\ClassicCounter"
set /p "destination=Destination folder (Enter for default: C:\Games\ClassicCounter): "
if "%destination%"=="" set "destination=%defaultDest%"

if not exist "%destination%" (
  mkdir "%destination%"
)

robocopy "%publishDir%" "%destination%" /e /r:1 /w:1 /xf *.pdb >nul
if errorlevel 8 (
  echo Copy failed.
  exit /b 1
)

echo Copied to: %destination% (without .pdb files)
timeout /t 3 >nul
