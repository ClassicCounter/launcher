<p align="center">
 <h2 align="center">ClassicCounter Wauncher</h2>
 <p align="center">
   Wauncher for ClassicCounter with Discord RPC, Auto-Updates and More!
   <br/>
   Written in C# using .NET 8.
 </p>
</p>

[![Downloads][downloads-shield]][downloads-url]
[![Stars][stars-shield]][stars-url]
[![Issues][issues-shield]][issues-url]
[![MIT License][license-shield]][license-url]

> [!IMPORTANT]
> .NET Runtime 8 is required to run the Wauncher. Download it from [**here**](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-8.0.11-windows-x64-installer).

## Settings
- Validation behavior is controlled in the GUI.
- Use `Verify Game Files` from the launch button drop-up menu when you want a full file check.

## Build / Publish
- Build: `dotnet build Wauncher/Wauncher.csproj -c Release`
- Publish: `dotnet publish Wauncher/Wauncher.csproj -c Release -r win-x64 --self-contained false`
- Quick publish script: `publish.bat` (builds + hashes + optional copy target)

## Packages Used
- [CSGSI](https://github.com/rakijah/CSGSI) by [rakijah](https://github.com/rakijah)
- [DiscordRichPresence](https://github.com/Lachee/discord-rpc-csharp) by [Lachee](https://github.com/Lachee)
- [Downloader](https://github.com/bezzad/Downloader) by [bezzad](https://github.com/bezzad)
- [Gameloop.Vdf](https://github.com/shravan2x/Gameloop.Vdf) by [shravan2x](https://github.com/shravan2x)
- [Refit](https://github.com/reactiveui/refit) by [ReactiveUI](https://github.com/reactiveui)
- [Spectre.Console](https://github.com/spectreconsole/spectre.console) by [Spectre Console](https://github.com/spectreconsole)

[downloads-shield]: https://img.shields.io/github/downloads/classiccounter/launcher/total.svg?style=for-the-badge
[downloads-url]: https://github.com/classiccounter/launcher/releases/latest
[stars-shield]: https://img.shields.io/github/stars/classiccounter/launcher.svg?style=for-the-badge
[stars-url]: https://github.com/classiccounter/launcher/stargazers
[issues-shield]: https://img.shields.io/github/issues/classiccounter/launcher.svg?style=for-the-badge
[issues-url]: https://github.com/classiccounter/launcher/issues
[license-shield]: https://img.shields.io/github/license/classiccounter/launcher.svg?style=for-the-badge
[license-url]: https://github.com/classiccounter/launcher/blob/main/LICENSE.txt
