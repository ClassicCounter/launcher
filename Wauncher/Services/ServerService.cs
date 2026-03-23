using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Net.Http;
using System.Text.Json;
using Avalonia.Threading;
using Wauncher.Utils;
using Wauncher.ViewModels;

namespace Wauncher.Services
{
    public class ServerService : IServerService
    {
        private DispatcherTimer? _serverRefreshTimer;
        private int _serverRefreshInProgress;
        private bool _started;
        private readonly HttpClient _httpClient;
        private const string SERVER_LIST_URL = "https://raw.githubusercontent.com/edd13s/ClassicCounter-Servers/refs/heads/main/servers.json";

        public bool IsOfflineMode => !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();

        public ObservableCollection<ServerInfo> Servers { get; } = new()
        {
            // Start with "None" server so UI always has something
            new ServerInfo { Name = "None", IpPort = "", IsOnline = false }
        };

        public ServerService()
        {
            _httpClient = new HttpClient();
        }

        public void Start()
        {
            if (_started)
                return;

            _started = true;
            _ = RefreshServersSafeAsync();
            _serverRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _serverRefreshTimer.Tick += async (_, _) => await RefreshServersSafeAsync();
            _serverRefreshTimer.Start();
        }

        public async Task RefreshServersAsync()
        {
            if (IsOfflineMode)
            {
                foreach (var s in Servers.Where(s => !s.IsNone))
                {
                    s.IsOnline = false;
                    s.Players = 0;
                    s.MaxPlayers = 0;
                    s.Map = "";
                }
                return;
            }

            // Load servers from web API
            await LoadServersFromWebAsync();

            // Query live server status
            await ServerQuery.RefreshServers(Servers.Where(s => !s.IsNone));

            // Re-order by player count descending; None always stays at index 0
            var sorted = Servers.Where(s => !s.IsNone)
                                .OrderByDescending(s => s.Players)
                                .ToList();
            int insertAt = 1;
            foreach (var server in sorted)
            {
                int from = Servers.IndexOf(server);
                if (from != insertAt)
                    Servers.Move(from, insertAt);
                insertAt++;
            }
        }

        private async Task LoadServersFromWebAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Fetching servers from: {SERVER_LIST_URL}");
                var response = await _httpClient.GetAsync(SERVER_LIST_URL);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Raw JSON: {json}");
                
                var serverData = JsonSerializer.Deserialize<ServerData[]>(json);
                System.Diagnostics.Debug.WriteLine($"Deserialized {serverData?.Length ?? 0} servers");
                
                if (serverData != null)
                {
                    // Clear existing servers except "None"
                    var existingServers = Servers.Where(s => !s.IsNone).ToList();
                    foreach (var server in existingServers)
                    {
                        Servers.Remove(server);
                    }
                    
                    // Add servers from web API (skip "None" from JSON since we already have it)
                    foreach (var server in serverData.Where(s => s.name != "None"))
                    {
                        System.Diagnostics.Debug.WriteLine($"Adding server: {server.name} - {server.ipPort}");
                        Servers.Add(new ServerInfo 
                        { 
                            Name = server.name,
                            IpPort = server.ipPort,
                            MaxPlayers = server.maxPlayers,
                            IsOnline = true
                        });
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Total servers in collection: {Servers.Count}");
                }
            }
            catch (Exception ex)
            {
                // Log error and fallback to default servers
                System.Diagnostics.Debug.WriteLine($"Failed to load servers from web: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                await LoadDefaultServersAsync();
            }
        }

        private async Task LoadDefaultServersAsync()
        {
            // Clear existing servers except "None"
            var existingServers = Servers.Where(s => !s.IsNone).ToList();
            foreach (var server in existingServers)
            {
                Servers.Remove(server);
            }

            // Ensure "None" server exists
            if (!Servers.Any(s => s.IsNone))
            {
                Servers.Insert(0, new ServerInfo { Name = "None", IpPort = "", IsOnline = false });
            }

            // No default servers - rely entirely on web API
            // This forces users to configure the SERVER_LIST_URL
        }

        public async Task RefreshServersSafeAsync()
        {
            if (Interlocked.Exchange(ref _serverRefreshInProgress, 1) == 1)
                return;

            try
            {
                await RefreshServersAsync();
            }
            finally
            {
                Interlocked.Exchange(ref _serverRefreshInProgress, 0);
            }
        }
    }

    // JSON data structure for server list
    public class ServerData
    {
        public string name { get; set; } = "";
        public string ipPort { get; set; } = "";
        public int maxPlayers { get; set; }
    }
}
