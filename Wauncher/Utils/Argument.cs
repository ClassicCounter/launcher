namespace Wauncher.Utils
{
    public static class Argument
    {
        private static readonly List<string> _additionalArguments = new();

        public static void AddArgument(string argument)
        {
            if (!_additionalArguments.Any(a => string.Equals(a, argument, StringComparison.OrdinalIgnoreCase)))
                _additionalArguments.Add(argument);
        }

        public static void ClearAdditionalArguments()
        {
            _additionalArguments.Clear();
        }

        public static bool HasProtocolCommand() =>
            Environment.GetCommandLineArgs().Any(arg =>
                arg.StartsWith("cc://", StringComparison.OrdinalIgnoreCase));

        public static List<string> GenerateGameArguments()
        {
            IEnumerable<string> launcherArguments = Environment.GetCommandLineArgs();
            List<string> gameArguments = new();

            foreach (string arg in launcherArguments)
            {
                if (!arg.StartsWith("cc://", StringComparison.OrdinalIgnoreCase))
                    continue;

                string protocolArgument = arg.Replace("cc://", "", StringComparison.OrdinalIgnoreCase);
                string[] protocolArguments = protocolArgument.Split('/');
                if (protocolArguments.Length < 2)
                    continue;

                switch (protocolArguments[0])
                {
                    case "connect":
                        gameArguments.Add("+connect");
                        gameArguments.Add(protocolArguments[1]);
                        break;
                }
            }

            gameArguments.AddRange(_additionalArguments);
            return gameArguments;
        }
    }
}
