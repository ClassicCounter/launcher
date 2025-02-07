dotnet publish -c Release
certutil -hashfile "CCLauncher-GUI.Desktop\bin\Release\net8.0\win-x64\publish\launcher.exe" MD5
pause