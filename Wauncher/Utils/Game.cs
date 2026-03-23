using CSGSI;
using CSGSI.Nodes;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Wauncher.Utils
{
    public static class Game
    {
        private static Process? _process;
        private static GameStateListener? _listener;
        private static int _port;
        private static MapNode? _node;

        private static string _map = "main_menu";
        private static int _scoreCT = 0;
        private static int _scoreT = 0;
        private static string? _pendingConnectTarget;
        private static int _pendingNetConPort;
        private static int _pendingConnectTriggered;

        public static bool IsRunning()
        {
            try
            {
                return Process.GetProcessesByName("csgo").Length > 0 ||
                       Process.GetProcessesByName("cc").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public static void QueueDeferredConnect(string ipPort)
        {
            _pendingConnectTarget = string.IsNullOrWhiteSpace(ipPort) ? null : ipPort.Trim();
            _pendingNetConPort = string.IsNullOrWhiteSpace(_pendingConnectTarget) ? 0 : GeneratePort();
            _pendingConnectTriggered = 0;
        }

        public static async Task<bool> Launch()
        {
            List<string> arguments = Argument.GenerateGameArguments();
            if (arguments.Count > 0) Terminal.Print($"Arguments: {string.Join(" ", arguments)}");

            var settings = ViewModels.SettingsWindowViewModel.LoadGlobal();
            string directory = Path.GetDirectoryName(Services.GetExePath()) ?? Directory.GetCurrentDirectory();
            Terminal.Print($"Directory: {directory}");

            string gameStatePath = Path.Combine(directory, "csgo", "cfg", "gamestate_integration_cc.cfg");

            if (settings.DiscordRpc || !string.IsNullOrWhiteSpace(_pendingConnectTarget))
            {
                EnsureGameStateListenerStarted();

                try
                {
                    string gameStateContents = $$"""
"ClassicCounter"
{
	"uri"                         "http://localhost:{{_port}}"
	"timeout"                     "5.0"
	"auth"
	{
		"token"                    "ClassicCounter {{Version.Current}}"
	}
	"data"
	{
		"provider"                 "1"
		"map"                      "1"
		"round"                    "1"
		"player_id"                "1"
		"player_weapons"           "1"
		"player_match_stats"       "1"
		"player_state"             "1"
		"allplayers_id"            "1"
		"allplayers_state"         "1"
		"allplayers_match_stats"   "1"
	}
}
""";
                    await File.WriteAllTextAsync(gameStatePath, gameStateContents);
                }
                catch
                {
                    Terminal.Error("(!) \"/csgo/cfg/gamestate_integration_cc.cfg\" not found in the current directory!");
                }
            }
            else if (File.Exists(gameStatePath))
            {
                File.Delete(gameStatePath);
            }

            _process = new Process();

            string gameExe = "csgo.exe";
            _process.StartInfo.FileName = Path.Combine(directory, gameExe);
            _process.StartInfo.Arguments = string.Join(" ", arguments);
            _process.StartInfo.WorkingDirectory = directory;

            if (_pendingNetConPort > 0)
            {
                _process.StartInfo.Arguments = string.IsNullOrWhiteSpace(_process.StartInfo.Arguments)
                    ? $"-netconport {_pendingNetConPort}"
                    : $"{_process.StartInfo.Arguments} -netconport {_pendingNetConPort}";
            }

            if (!File.Exists(_process.StartInfo.FileName))
            {
                Terminal.Error($"(!) {gameExe} not found in the current directory!");
                ConsoleManager.ShowError($"{gameExe} not found in the current directory!\n\nPlease make sure the launcher and game files are in the same folder.");
                return false;
            }

            return _process.Start();
        }

        public static async Task Monitor()
        {
            if (_process == null) return;

            int missingProcessPolls = 0;

            while (true)
            {
                bool trackedProcessRunning = false;
                try
                {
                    trackedProcessRunning = _process != null && !_process.HasExited;
                }
                catch
                {
                    trackedProcessRunning = false;
                }

                bool anyGameProcessRunning = IsRunning();
                if (!trackedProcessRunning && !anyGameProcessRunning)
                {
                    missingProcessPolls++;
                    if (missingProcessPolls >= 2)
                        break;
                }
                else
                {
                    missingProcessPolls = 0;
                }

                if (_node != null && _node.Name.Trim().Length != 0)
                {
                    bool isMainMenu = string.Equals(_node.Name, "main_menu", StringComparison.OrdinalIgnoreCase);
                    if (!isMainMenu)
                    {
                        if (_map != _node.Name)
                        {
                            _map = _node.Name;
                            _scoreCT = _node.TeamCT.Score;
                            _scoreT = _node.TeamT.Score;

                            Discord.SetDetails(_map);
                            Discord.SetState($"Score → {_scoreCT}:{_scoreT}");
                            Discord.SetTimestamp(DateTime.UtcNow);
                            Discord.SetLargeArtwork($"https://assets.classiccounter.cc/maps/default/{_map}.jpg");
                            Discord.SetSmallArtwork("icon");
                            Discord.Update();
                        }

                        if (_scoreCT != _node.TeamCT.Score || _scoreT != _node.TeamT.Score)
                        {
                            _scoreCT = _node.TeamCT.Score;
                            _scoreT = _node.TeamT.Score;

                            Discord.SetState($"Score → {_scoreCT}:{_scoreT}");
                            Discord.Update();
                        }
                    }
                    else
                    {
                        _map = "main_menu";
                        _scoreCT = 0;
                        _scoreT = 0;
                    }
                }
                else if (_map != "main_menu")
                {
                    _map = "main_menu";
                    _scoreCT = 0;
                    _scoreT = 0;

                    Discord.SetDetails("In Main Menu");
                    Discord.SetState(null);
                    Discord.SetTimestamp(DateTime.UtcNow);
                    Discord.SetLargeArtwork("icon");
                    Discord.SetSmallArtwork(null);
                    Discord.Update();
                }

                await Task.Delay(2000);
            }

            _process = null;
            _node = null;
            _map = "main_menu";
            _scoreCT = 0;
            _scoreT = 0;
            _pendingConnectTarget = null;
            _pendingNetConPort = 0;
            _pendingConnectTriggered = 0;
        }

        private static void EnsureGameStateListenerStarted()
        {
            if (_listener != null)
                return;

            _port = GeneratePort();

            var listener = new GameStateListener($"http://localhost:{_port}/");
            listener.NewGameState += OnNewGameState;

            if (!listener.Start())
            {
                listener.NewGameState -= OnNewGameState;
                throw new InvalidOperationException("Couldn't start Wauncher's local game state listener.");
            }

            _listener = listener;
        }

        private static int GeneratePort()
        {
            int port = new Random().Next(1024, 65536);

            IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
            while (properties.GetActiveTcpConnections().Any(x => x.LocalEndPoint.Port == port))
            {
                port = new Random().Next(1024, 65536);
            }

            return port;
        }

        public static void OnNewGameState(GameState gs)
        {
            _node = gs.Map;

            if (!string.IsNullOrWhiteSpace(_pendingConnectTarget) &&
                _pendingNetConPort > 0 &&
                Interlocked.Exchange(ref _pendingConnectTriggered, 1) == 0)
            {
                _ = Task.Run(SendDeferredConnectWhenReadyAsync);
            }
        }

        private static async Task SendDeferredConnectWhenReadyAsync()
        {
            string? target = _pendingConnectTarget;
            int port = _pendingNetConPort;
            if (string.IsNullOrWhiteSpace(target) || port <= 0)
            {
                _pendingConnectTriggered = 0;
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3));

                for (int attempt = 0; attempt < 10; attempt++)
                {
                    try
                    {
                        using var client = new TcpClient();
                        await client.ConnectAsync("127.0.0.1", port);
                        using var stream = client.GetStream();
                        using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\n" };
                        await writer.WriteLineAsync($"connect {target}");
                        _pendingConnectTarget = null;
                        _pendingNetConPort = 0;
                        return;
                    }
                    catch
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                }
            }
            finally
            {
                _pendingConnectTriggered = 0;
            }
        }
    }
}
