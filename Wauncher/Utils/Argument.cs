namespace Wauncher.Utils
{
    public static class Argument
    {
        private static readonly List<string> _additionalArguments = new();
        private static bool _protocolConnectConsumed;

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

        public static string? GetProtocolConnectTarget()
        {
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (!arg.StartsWith("cc://", StringComparison.OrdinalIgnoreCase))
                    continue;

                string protocolArgument = arg.Replace("cc://", "", StringComparison.OrdinalIgnoreCase);
                string[] protocolArguments = protocolArgument.Split('/');
                if (protocolArguments.Length < 2)
                    continue;

                if (!string.Equals(protocolArguments[0], "connect", StringComparison.OrdinalIgnoreCase))
                    continue;

                var target = Uri.UnescapeDataString(protocolArguments[1]).Trim();
                return string.IsNullOrWhiteSpace(target) ? null : target;
            }

            return null;
        }

        public static void ConsumeProtocolConnectTarget()
        {
            _protocolConnectConsumed = true;
        }

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
                        if (_protocolConnectConsumed)
                            break;

                        gameArguments.Add("+connect");
                        gameArguments.Add(Uri.UnescapeDataString(protocolArguments[1]));
                        break;
                }
            }

            gameArguments.AddRange(_additionalArguments);
            return gameArguments;
        }
    }
}
