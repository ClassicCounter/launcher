dotnet publish Launcher -c Release
dotnet publish Wauncher -c Release
certutil -hashfile "Launcher\bin\Release\net8.0-windows7.0\win-x64\publish\launcher.exe" MD5
certutil -hashfile "Wauncher\bin\Release\net8.0-windows7.0\win-x64\publish\wauncher.exe" MD5
pause