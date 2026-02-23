using CSGSI;
using CSGSI.Nodes;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Launcher.Utils
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

        public static async Task<bool> Launch()
        {
            List<string> arguments = Argument.GenerateGameArguments();
            if (arguments.Count > 0) Terminal.Print($"Arguments: {string.Join(" ", arguments)}");

            string directory = Directory.GetCurrentDirectory();
            Terminal.Print($"Directory: {directory}");

            string gameStatePath = $"{directory}/csgo/cfg/gamestate_integration_cc.cfg";
            
            if (!Argument.Exists("--disable-rpc"))
            {
                _port = GeneratePort();

                if (Argument.Exists("--debug-mode"))
                    Terminal.Debug($"Starting Game State Integration with TCP port {_port}.");

                _listener = new($"http://localhost:{_port}/");
                _listener.NewGameState += OnNewGameState;
                _listener.Start();

                try {
                await File.WriteAllTextAsync(gameStatePath,
@"""ClassicCounter""
{
	""uri""                         ""http://localhost:" + _port + @"""
	""timeout""                     ""5.0""
	""auth""
	{
		""token""				    """ + $"ClassicCounter {Version.Current}" + @"""
	}
	""data""
	{
		""provider""              	""1""
		""map""                   	""1""
		""round""                 	""1""
		""player_id""				""1""
		""player_weapons""			""1""
		""player_match_stats""		""1""
		""player_state""			""1""
		""allplayers_id""			""1""
		""allplayers_state""		""1""
		""allplayers_match_stats""	""1""
	}
}"
                );
                }
                catch
                {
                    Terminal.Error($"(!) \"/csgo/cfg/gamestate_integration_cc.cfg\" not found in the current directory!");
                }
            }
            else if (File.Exists(gameStatePath)) File.Delete(gameStatePath);

            _process = new Process();
            bool useGC = Argument.Exists("--gc");

            if (Argument.Exists("--debug-mode"))
                Terminal.Debug($"Launching the game with{(useGC ? "" : "out")} Game Coordinator...");

            string gameExe = useGC ? "cc.exe" : "csgo.exe";
            _process.StartInfo.FileName = $"{directory}\\{gameExe}";
            _process.StartInfo.Arguments = string.Join(" ", arguments);

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
            while (true)
            {
                if (_process == null)
                    break;

                try
                {
                    Process.GetProcessById(_process.Id);
                }
                catch
                {
                    Environment.Exit(1);
                }

                if (_node != null && _node.Name.Trim().Length != 0)
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

        public static void OnNewGameState(GameState gs) => _node = gs.Map;
    }
}
