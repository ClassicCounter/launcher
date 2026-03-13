using Launcher.Utils;
using Spectre.Console;
using System.Diagnostics;

using Debug = Launcher.Utils.Debug;
using Version = Launcher.Utils.Version;

Console.Clear();

if (Debug.Enabled())
    Terminal.Debug("Cleaning up any .7z files at startup...");
DownloadManager.Cleanup7zFiles();

if (!Argument.Exists("--disable-rpc"))
    Discord.Init();

Terminal.Init();

await Task.Delay(1000);

// this int saves total download progress (duh)
// its used for whenever HandlePatches is called so that the display for downloading files doesnt glitch the fuck out

// TODO: Remove this crap ^ and come up with something else
int totalDownloadProgress = 0;

string updaterPath = $"{Directory.GetCurrentDirectory()}/updater.exe";
if (File.Exists(updaterPath))
{
    if (Debug.Enabled())
        Terminal.Debug("Found and deleting: updater.exe");

    try
    {
        File.Delete(updaterPath);
    }
    catch
    {
        if (Debug.Enabled())
            Terminal.Debug("Couldn't delete updater.exe, possibly due to missing permissions.");
    }
}

if (!Argument.Exists("--skip-updates"))
{
    string latestVersion = await Version.GetLatestVersion();

    if (Version.Current != latestVersion)
    {
        Terminal.Print("You're using an outdated launcher - updating...");
        await AnsiConsole
            .Status()
            .SpinnerStyle(Style.Parse("gray"))
            .StartAsync("Downloading auto-updater.", async ctx =>
        {
            try
            {
                await DownloadManager.DownloadUpdater(updaterPath);

                if (!File.Exists(updaterPath))
                {
                    if (Debug.Enabled())
                        Terminal.Debug("Updater.exe that was just downloaded doesn't exist, possibly due to missing permissions.");

                    return;
                }

                Process updaterProcess = new Process();
                updaterProcess.StartInfo.FileName = updaterPath;
                updaterProcess.StartInfo.Arguments = $"--version={latestVersion} {string.Join(" ", Argument.GenerateGameArguments(true))}";
                updaterProcess.Start();
            }
            catch
            {
                Terminal.Error("Couldn't download or launch auto-updater. Closing launcher in 5 seconds.");
                await Task.Delay(5000);
            }

            Environment.Exit(1);
        });
    }
    else
        Terminal.Success("Launcher is up-to-date!");
}

string directory = Directory.GetCurrentDirectory();

Dependencies dependenciesToInstall = new Dependencies(false, new List<Dependency>(), new List<Dependency>());

if (Argument.Exists("--ssl-bypass"))
    Terminal.Warning("Bypassing certificate authority validations...");

if (Argument.Exists("--install-dependencies"))
{
    bool success = new Boolean();
    Terminal.Print("Forcing downloading and installing dependencies...");
    await AnsiConsole
    .Status()
    .SpinnerStyle(Style.Parse("blue"))
    .StartAsync("Downloading dependencies...", async ctx =>
    {
        List<Dependency> dependencies = await DependencyManager.Get();
        dependenciesToInstall = await DownloadManager.DownloadDependencies(ctx, dependencies);
    });

    if (!dependenciesToInstall.Success) await AnsiConsole
    .Status()
    .SpinnerStyle(Style.Parse("green"))
    .StartAsync("Installing dependencies...", async ctx =>
    {
        success = await DependencyManager.Install(ctx, dependenciesToInstall);
    });
    if (success)
    {
        Terminal.Success("Finished dependency installing! Closing launcher.");
        Terminal.SteamHappy();
    }
    else
        Terminal.Error("No dependencies were installed. Closing launcher.");
    await Task.Delay(3000);
    Environment.Exit(0);
    return;
}

if (!File.Exists($"{directory}/csgo.exe"))
{
    // if there's a .7z.001 file, start downloading
    if (Directory.GetFiles(directory, "*.7z.001").Length > 0)
    {
        await AnsiConsole
        .Status()
        .SpinnerStyle(Style.Parse("gray"))
        .StartAsync("Downloading full game...", async ctx =>
        {
            await DownloadManager.DownloadFullGame(ctx);
        });
    }
    else
    {
        Terminal.Error("(!) csgo.exe not found in the current directory!");
        Terminal.Warning($"Game files will be installed to: {directory}");
        Terminal.Warning("This will download approximately 7GB of data. Make sure you have enough disk space.");
        AnsiConsole.Markup($"[orange1]Classic[/][blue]Counter[/] [grey50]|[/] [grey82]Would you like to download the full game? (y/n): [/]");
        var response = Console.ReadKey(true);
        Console.WriteLine(response.KeyChar);
        Console.WriteLine();

        if (char.ToLower(response.KeyChar) == 'y')
        {
            string? rootPath = Path.GetPathRoot(directory);
            if (rootPath == null)
            {
                Terminal.Error("Could not determine drive root path!");
                return;
            }

            DriveInfo driveInfo = new DriveInfo(rootPath);
            long requiredSpace = 24L * 1024 * 1024 * 1024; // 24 GB in bytes

            if (driveInfo.AvailableFreeSpace < requiredSpace)
            {
                Terminal.Error("(!) Not enough disk space available!");
                Terminal.Error($"Required: 24 GB, Available: {driveInfo.AvailableFreeSpace / (1024.0 * 1024 * 1024):F2} GB");
                Terminal.Error("Please free up some disk space and try again. Closing launcher in 10 seconds...");
                await Task.Delay(10000);
                Environment.Exit(1);
                return;
            }

            await AnsiConsole
            .Status()
            .SpinnerStyle(Style.Parse("gray"))
            .StartAsync("Downloading full game...", async ctx =>
            {
                await DownloadManager.DownloadFullGame(ctx);
            });

            bool success = false;

            await AnsiConsole
            .Status()
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync("Downloading dependencies...", async ctx =>
            {
                List<Dependency> dependencies = await DependencyManager.Get();
                dependenciesToInstall = await DownloadManager.DownloadDependencies(ctx, dependencies);
            });

            if (dependenciesToInstall != null) await AnsiConsole
            .Status()
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync("Installing dependencies...", async ctx =>
            {
                success = await DependencyManager.Install(ctx, dependenciesToInstall);
            });
            if (success)
            {
                Terminal.Success("Finished dependency installing!");
                
                Terminal.Print("Creating shortcuts...");
                string launcherPath = Environment.ProcessPath ?? "";
                string csgoPath = $"{directory}/csgo.exe";
                
                if (!string.IsNullOrEmpty(launcherPath))
                {
                    ShortcutManager.CreateDesktopShortcut(launcherPath, "ClassicCounter Launcher", "Launch ClassicCounter");
                    ShortcutManager.CreateStartMenuShortcut(launcherPath, "ClassicCounter Launcher", "Launch ClassicCounter");
                }
                
                if (File.Exists(csgoPath))
                {
                    ShortcutManager.CreateDesktopShortcut(csgoPath, "ClassicCounter", "Play ClassicCounter");
                    ShortcutManager.CreateStartMenuShortcut(csgoPath, "ClassicCounter", "Play ClassicCounter");
                    Terminal.Success("Created shortcuts!");
                }
                
                AnsiConsole.Markup($"[orange1]Classic[/][blue]Counter[/] [grey50]|[/] [grey82]Would you like to add ClassicCounter to your Steam library? (y/n): [/]");
                var steamResponse = Console.ReadKey(true);
                Console.WriteLine(steamResponse.KeyChar);
                Console.WriteLine();
                
                if (char.ToLower(steamResponse.KeyChar) == 'y')
                {
                    ShortcutManager.AddToSteamLibrary(csgoPath, "ClassicCounter");
                }
            }
            else
                Terminal.Error("No dependencies were installed.");
        }
        else
        {
            Terminal.Error("Game files are required to run ClassicCounter. Closing launcher in 10 seconds...");
            await Task.Delay(10000);
            Environment.Exit(1);
        }
    }
}

if (!Argument.Exists("--skip-validating"))
{
    await AnsiConsole
    .Status()
    .SpinnerStyle(Style.Parse("gray"))
    .StartAsync("Validating files...", async ctx =>
    {
        bool validateAll = Argument.Exists("--validate-all");

        if (validateAll)
        {
            // First validate all game files
            ctx.Status = "Validating game files...";
            Patches gameFiles = await PatchManager.ValidatePatches(true);
            if (gameFiles.Success)
            {
                Terminal.Print("Finished validating game files!");
                if (gameFiles.Missing.Count > 0 || gameFiles.Outdated.Count > 0)
                {
                    if (gameFiles.Missing.Count > 0)
                        Terminal.Warning($"Found {gameFiles.Missing.Count} missing {(gameFiles.Missing.Count == 1 ? "game file" : "game files")}!");

                    if (gameFiles.Outdated.Count > 0)
                        Terminal.Warning($"Found {gameFiles.Outdated.Count} outdated {(gameFiles.Outdated.Count == 1 ? "game file" : "game files")}!");

                    Terminal.Print("If you're stuck at downloading - reopen the launcher.");

                    await DownloadManager.HandlePatches(gameFiles, ctx, true, totalDownloadProgress);
                    totalDownloadProgress += gameFiles.Missing.Count + gameFiles.Outdated.Count;
                }
                else
                {
                    Terminal.Success("Game files are up-to-date!");
                }
            }
            else
            {
                Terminal.Error("(!) Couldn't validate game files!");
                Terminal.Error("(!) Is your ISP blocking CloudFlare? Check your Network settings.");
                return;
            }

            // Then validate patches
            ctx.Status = "Validating patches...";
            Terminal.Print("\nNow checking for new patches...");
        }

        // Regular patch validation
        Patches patches = await PatchManager.ValidatePatches(false);
        if (patches.Success)
        {
            Terminal.Print("Finished validating patches!");
            if (patches.Missing.Count == 0 && patches.Outdated.Count == 0)
            {
                Terminal.Success("Patches are up-to-date!");
                return;
            }
        }
        else
        {
            Terminal.Error("(!) Couldn't validate patches!");
            Terminal.Error("(!) Is your ISP blocking CloudFlare? Check your Network settings.");
            if (!Argument.Exists("--patch-only"))
            {
                Terminal.Warning("Launching ClassicCounter anyways...");
            }
            return;
        }

        if (patches.Missing.Count > 0)
            Terminal.Warning($"Found {patches.Missing.Count} missing {(patches.Missing.Count == 1 ? "patch" : "patches")}!");

        if (patches.Outdated.Count > 0)
            Terminal.Warning($"Found {patches.Outdated.Count} outdated {(patches.Outdated.Count == 1 ? "patch" : "patches")}!");

        Terminal.Print("If you're stuck at downloading patches - reopen the launcher.");

        await DownloadManager.HandlePatches(patches, ctx, false, totalDownloadProgress);
        totalDownloadProgress += patches.Missing.Count + patches.Outdated.Count;

        // Cleanup temporary files
        if (Debug.Enabled())
            Terminal.Debug("Cleaning up temporary files...");

        try
        {
            // Try to delete the 7z.dll
            string launcherDllPath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? "", "7z.dll");
            if (File.Exists(launcherDllPath))
            {
                try
                {
                    File.Delete(launcherDllPath);
                    if (Debug.Enabled())
                        Terminal.Debug($"Deleted 7z.dll: {launcherDllPath}");
                }
                catch (Exception ex)
                {
                    if (Debug.Enabled())
                        Terminal.Debug($"Failed to delete 7z.dll: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            if (Debug.Enabled())
                Terminal.Debug($"Cleanup failed: {ex.Message}");
        }
    });
}

if (Argument.Exists("--patch-only"))
{
    Terminal.Success("Finished patch validation and downloads! Closing launcher.");
    await Task.Delay(3000);
    Environment.Exit(0);
    return;
}

if (Debug.Enabled())
    Terminal.Debug("Cleaning up any .7z files...");
DownloadManager.Cleanup7zFiles();

if (Argument.Exists("--create-shortcuts"))
{
    Terminal.Print("Creating shortcuts...");
    string launcherPath = Environment.ProcessPath ?? "";
    string csgoPath = $"{directory}/csgo.exe";
    
    if (!string.IsNullOrEmpty(launcherPath))
    {
        ShortcutManager.CreateDesktopShortcut(launcherPath, "ClassicCounter Launcher", "Launch ClassicCounter");
        ShortcutManager.CreateStartMenuShortcut(launcherPath, "ClassicCounter Launcher", "Launch ClassicCounter");
        Terminal.Success("Created launcher shortcuts!");
    }
    
    if (File.Exists(csgoPath))
    {
        ShortcutManager.CreateDesktopShortcut(csgoPath, "ClassicCounter", "Play ClassicCounter");
        ShortcutManager.CreateStartMenuShortcut(csgoPath, "ClassicCounter", "Play ClassicCounter");
        Terminal.Success("Created game shortcuts!");
    }
}

if (Argument.Exists("--add-to-steam"))
{
    Terminal.Print("Adding ClassicCounter to Steam library...");
    string csgoPath = $"{directory}/csgo.exe";
    
    if (File.Exists(csgoPath))
    {
        ShortcutManager.AddToSteamLibrary(csgoPath, "ClassicCounter");
    }
    else
    {
        Terminal.Error("csgo.exe not found! Cannot add to Steam library.");
    }
}

bool launched = await Game.Launch();
if (!launched)
{
    Terminal.Error("ClassicCounter didn't launch properly. Make sure launcher.exe and csgo.exe are in the same directory. Closing launcher in 10 seconds.");
    await Task.Delay(10000);
}
else if (Argument.Exists("--disable-rpc"))
{
    Terminal.Success("Launched ClassicCounter! Closing launcher in 5 seconds.");
    await Task.Delay(5000);
}
else
{
    Terminal.Success("Launched ClassicCounter! Launcher will minimize in 5 seconds to manage Discord RPC.");
    await Task.Delay(5000);

    ConsoleManager.HideConsole();
    Discord.SetDetails("In Main Menu");
    Discord.Update();
    await Game.Monitor();
}