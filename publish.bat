@echo off
for /f %%a in ('echo prompt $E^| cmd') do set "ESC=%%a"
setlocal

echo =============================
echo %ESC%[42mBuilding Wauncher...%ESC%[0m
dotnet publish Wauncher\Wauncher.csproj -c Release -r win-x64 --self-contained false
if errorlevel 1 (
  echo Publish failed.
  exit /b 1
)

echo =============================
echo %ESC%[41mHashing wauncher.exe...%ESC%[0m
certutil -hashfile "Wauncher\bin\Release\net8.0-windows7.0\win-x64\publish\wauncher.exe" MD5

echo =============================
echo %ESC%[1;43mCopying Wauncher publish output...%ESC%[0m
set "defaultDest=C:\Games\ClassicCounter"
set /p "destination=Destination folder (Enter for default: C:\Games\ClassicCounter): "
if "%destination%"=="" set "destination=%defaultDest%"

if not exist "%destination%" (
  mkdir "%destination%"
)

xcopy "Wauncher\bin\Release\net8.0-windows7.0\win-x64\publish\*" "%destination%\" /e /y /i >nul
echo Copied to: %destination%
timeout /t 3 >nul
