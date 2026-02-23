namespace Launcher.Utils
{
    public static class Argument
    {
        private static List<string> _launcherArguments = new()
        {
            "--debug-mode",
            "--skip-updates",
            "--skip-validating",
            "--validate-all",
            "--patch-only",
            "--gc",
            "--disable-rpc",
            "--install-dependencies",
            "--protocol-command"
        };

        private static List<string> _additionalArguments = new();
        public static void AddArgument(string argument)
        {
            if (!_additionalArguments.Contains(argument.ToLowerInvariant()))
            {
                _additionalArguments.Add(argument.ToLowerInvariant());
            }
        }

        public static bool Exists(string argument)
        {
            IEnumerable<string> arguments = Environment.GetCommandLineArgs();

            foreach (string arg in arguments)
                if (arg.ToLowerInvariant() == argument) return true;

            return false;
        }

        public static List<string> GenerateGameArguments(bool passLauncherArguments = false)
        {
            IEnumerable<string> launcherArguments = Environment.GetCommandLineArgs();
            List<string> gameArguments = new();

            foreach (string arg in launcherArguments)
                if (arg.StartsWith("cc://"))
                {
                    string protocolArgument = arg.Replace("cc://", "");
                    string[] protocolArguments = protocolArgument.Split('/');
                    switch (protocolArguments[0])
                    {
                        case "connect":
                            gameArguments.Add("+" + protocolArguments[0]);
                            gameArguments.Add(protocolArguments[1]);
                            break;
                    }
                }
                else if ((passLauncherArguments || !_launcherArguments.Contains(arg.ToLowerInvariant()))
                    && !arg.EndsWith(".exe"))
                    gameArguments.Add(arg.ToLowerInvariant());

            gameArguments.AddRange(_additionalArguments);
            return gameArguments;
        }
    }
}
