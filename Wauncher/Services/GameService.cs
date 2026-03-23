using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Wauncher.Utils;

namespace Wauncher.Services
{
    public class GameService : IGameService
    {
        public async Task<bool> LaunchAsync(string? connectTarget = null, string? launchOptions = null)
        {
            ClearAdditionalArguments();
            
            // Add default arguments
            AddArgument("-novid");
            
            // Add custom launch options if provided
            if (!string.IsNullOrWhiteSpace(launchOptions))
            {
                foreach (var arg in ParseLaunchOptions(launchOptions))
                    AddArgument(arg);
            }

            // Add connect target if provided
            if (!string.IsNullOrEmpty(connectTarget))
            {
                var resolvedTarget = await ResolveConnectTarget(connectTarget);
                QueueDeferredConnect(resolvedTarget);
            }

            return await Task.Run(() => Game.Launch());
        }

        public async Task MonitorAsync()
        {
            await Game.Monitor();
        }

        public bool IsRunning()
        {
            return Game.IsRunning();
        }

        public void QueueDeferredConnect(string connectTarget)
        {
            Game.QueueDeferredConnect(connectTarget);
        }

        public void ClearAdditionalArguments()
        {
            Argument.ClearAdditionalArguments();
        }

        public void AddArgument(string argument)
        {
            Argument.AddArgument(argument);
        }

        private static IEnumerable<string> ParseLaunchOptions(string launchOptions)
        {
            if (string.IsNullOrWhiteSpace(launchOptions))
                yield break;

            var current = new StringBuilder();
            bool inQuotes = false;

            foreach (var ch in launchOptions)
            {
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(ch) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        yield return current.ToString();
                        current.Clear();
                    }
                    continue;
                }

                current.Append(ch);
            }

            if (current.Length > 0)
                yield return current.ToString();
        }

        private static async Task<string> ResolveConnectTarget(string ipPort)
        {
            if (string.IsNullOrWhiteSpace(ipPort))
                return ipPort;

            var parts = ipPort.Split(':', 2);
            if (parts.Length != 2 || IPAddress.TryParse(parts[0], out _))
                return ipPort.Trim();

            try
            {
                var addresses = await Dns.GetHostAddressesAsync(parts[0]);
                var address = addresses.FirstOrDefault();
                return address == null ? ipPort.Trim() : $"{address}:{parts[1]}";
            }
            catch
            {
                return ipPort.Trim();
            }
        }
    }
}
