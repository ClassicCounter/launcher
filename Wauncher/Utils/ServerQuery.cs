using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;

namespace Wauncher.Utils
{
    public class ServerQueryResult
    {
        public bool   Online     { get; set; }
        public int    Players    { get; set; }
        public int    MaxPlayers { get; set; }
        public string Map        { get; set; } = "";
    }

    public static class ServerQuery
    {
        private sealed class CachedHostEntry
        {
            public IPAddress[] Addresses { get; init; } = Array.Empty<IPAddress>();
            public DateTime ExpiresAtUtc { get; init; }
        }

        private static readonly byte[] A2S_INFO_REQUEST =
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0x54,
            0x53, 0x6F, 0x75, 0x72, 0x63, 0x65, 0x20, 0x45, 0x6E, 0x67,
            0x69, 0x6E, 0x65, 0x20, 0x51, 0x75, 0x65, 0x72, 0x79, 0x00
        };
        private static readonly ConcurrentDictionary<string, CachedHostEntry> _dnsCache = new();
        private static readonly TimeSpan DnsCacheDuration = TimeSpan.FromMinutes(5);

        public static async Task<ServerQueryResult> QueryAsync(string ipPort, int timeoutMs = 2000)
        {
            var result = new ServerQueryResult();
            try
            {
                var parts = ipPort.Split(':');
                string host = parts[0];
                int port    = int.Parse(parts[1]);

                var addresses = await GetHostAddressesCachedAsync(host);
                if (addresses.Length == 0) return result;

                var endpoint = new IPEndPoint(addresses[0], port);

                using var udp = new UdpClient();

                await udp.SendAsync(A2S_INFO_REQUEST, A2S_INFO_REQUEST.Length, endpoint);

                var cts  = new CancellationTokenSource(timeoutMs);
                var recv = await udp.ReceiveAsync(cts.Token);
                byte[] data = recv.Buffer;

                // Some servers respond with a challenge packet (0x41) before sending the real info.
                // Re-send the request with the 4-byte challenge appended and wait for the real reply.
                if (data.Length >= 9 && data[4] == 0x41)
                {
                    var challengeRequest = new byte[A2S_INFO_REQUEST.Length + 4];
                    Buffer.BlockCopy(A2S_INFO_REQUEST, 0, challengeRequest, 0, A2S_INFO_REQUEST.Length);
                    Buffer.BlockCopy(data, 5, challengeRequest, A2S_INFO_REQUEST.Length, 4);

                    cts = new CancellationTokenSource(timeoutMs);
                    await udp.SendAsync(challengeRequest, challengeRequest.Length, endpoint);
                    recv = await udp.ReceiveAsync(cts.Token);
                    data = recv.Buffer;
                }

                // A2S_INFO response: 4×0xFF + 0x49 header, then null-terminated strings:
                // [0] Server name  [1] Map  [2] Folder  [3] Game  then 2-byte AppID
                // then Players, MaxPlayers, ...
                if (data.Length < 6 || data[4] != 0x49) return result;

                int pos = 5;

                // Read each null-terminated string
                string ReadString()
                {
                    int start = pos;
                    while (pos < data.Length && data[pos] != 0x00) pos++;
                    var s = Encoding.UTF8.GetString(data, start, pos - start);
                    pos++; // skip null terminator
                    return s;
                }

                ReadString();              // [0] Server name — skip
                result.Map = ReadString(); // [1] Map name — keep
                ReadString();              // [2] Folder — skip
                ReadString();              // [3] Game — skip

                pos += 2; // AppID (2 bytes)

                if (pos + 2 > data.Length) return result;

                result.Players    = data[pos];
                result.MaxPlayers = data[pos + 1];
                result.Online     = true;
            }
            catch { /* timeout or unreachable = offline */ }

            return result;
        }

        public static async Task RefreshServers(IEnumerable<Wauncher.ViewModels.ServerInfo> servers)
        {
            var tasks = servers
                .Where(s => !s.IsNone)
                .Select(async s =>
                {
                    var r        = await QueryAsync(s.IpPort);
                    s.IsOnline   = r.Online;
                    s.Players    = r.Players;
                    s.MaxPlayers = r.MaxPlayers;
                    s.Map        = r.Map;
                    // Each setter fires its own targeted PropertyChanged notifications.
                });

            await Task.WhenAll(tasks);
        }

        private static async Task<IPAddress[]> GetHostAddressesCachedAsync(string host)
        {
            if (_dnsCache.TryGetValue(host, out var cached) &&
                cached.ExpiresAtUtc > DateTime.UtcNow &&
                cached.Addresses.Length > 0)
            {
                return cached.Addresses;
            }

            var addresses = await Dns.GetHostAddressesAsync(host);
            _dnsCache[host] = new CachedHostEntry
            {
                Addresses = addresses,
                ExpiresAtUtc = DateTime.UtcNow.Add(DnsCacheDuration)
            };
            return addresses;
        }
    }
}
