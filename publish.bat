dotnet publish Launcher -c Release
dotnet publish LauncherGUI.Desktop -c Release
certutil -hashfile "Launcher\bin\Release\net8.0-windows7.0\win-x64\publish\launcher.exe" MD5
certutil -hashfile "LauncherGUI.Desktop\bin\Release\net8.0\win-x64\publish\LauncherGUI.Desktop.exe" MD5
pause