@echo off
for /f %%a in ('echo prompt $E^| cmd') do set "ESC=%%a"
echo =============================
echo %ESC%[42mBuilding...%ESC%[0m
dotnet publish Launcher -c Release
dotnet publish Wauncher -c Release
echo =============================
echo %ESC%[41mHashing...%ESC%[0m
certutil -hashfile "Launcher\bin\Release\net8.0-windows7.0\win-x64\publish\launcher.exe" MD5
certutil -hashfile "Wauncher\bin\Release\net8.0-windows7.0\win-x64\publish\wauncher.exe" MD5
echo =============================
echo %ESC%[1;43mCopying...%ESC%[0m
set /p "destination=Copying destination (in quotations): "
xcopy "Wauncher\bin\Release\net8.0-windows7.0\win-x64\publish\" %destination% /e /y
timeout /t 5