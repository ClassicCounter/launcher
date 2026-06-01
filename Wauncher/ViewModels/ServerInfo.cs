using Avalonia.Threading;
using System.ComponentModel;

namespace Wauncher.ViewModels
{
    public class ServerInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name { get; set; } = "";
        public string IpPort { get; set; } = "";

        private int _players;
        private int _maxPlayers;
        private bool _isOnline;
        private string _map = "";

        public int Players
        {
            get => _players;
            set
            {
                if (_players == value) return;
                _players = value;
                Notify(nameof(Players), nameof(PlayerCount));
            }
        }

        public int MaxPlayers
        {
            get => _maxPlayers;
            set
            {
                if (_maxPlayers == value) return;
                _maxPlayers = value;
                Notify(nameof(MaxPlayers), nameof(PlayerCount));
            }
        }

        public bool IsOnline
        {
            get => _isOnline;
            set
            {
                if (_isOnline == value) return;
                _isOnline = value;
                Notify(nameof(IsOnline), nameof(DotColor));
            }
        }

        public string Map
        {
            get => _map;
            set
            {
                if (_map == value) return;
                _map = value;
                Notify(nameof(Map), nameof(MapDisplay), nameof(Subtitle));
            }
        }

        public bool IsNone => string.IsNullOrEmpty(IpPort);

        public string PlayerCount => IsNone ? "" : $"{Players}/{MaxPlayers}";
        public string DotColor => IsNone ? "#5A5A5A" : (IsOnline ? "#4CAF50" : "#F44336");
        public string NameColor => IsNone ? "#66FFFFFF" : "White";
        public string MapDisplay => (!IsNone && !string.IsNullOrEmpty(Map)) ? Map : "";
        public string Subtitle => IsNone ? "Play without selecting a server" : MapDisplay;
        public string MapImageUrl => (!IsNone && !string.IsNullOrEmpty(Map)) ? $"https://assets.classiccounter.cc/maps/gradient/{Map}.png" : "";
        public string Region => Name.StartsWith("EU", StringComparison.OrdinalIgnoreCase) ? "EU" :
                                Name.StartsWith("NA", StringComparison.OrdinalIgnoreCase) ? "NA" : "";
        public bool IsEu => Region == "EU";
        public bool IsNa => Region == "NA";

        public string DisplayName
        {
            get
            {
                if (IsNone) return Name;
                // Parse "EU | PUG | 64 Tick" to "EU | ClassicCounter | PUG | 64 Tick"
                var parts = Name.Split('|');
                if (parts.Length >= 3)
                {
                    var region = parts[0].Trim();
                    var gameMode = parts[1].Trim();
                    var tickRate = parts[2].Trim();
                    return $"{region} | ClassicCounter | {gameMode} | {tickRate}";
                }
                else if (parts.Length >= 2)
                {
                    var region = parts[0].Trim();
                    var gameMode = parts[1].Trim();
                    return $"{region} | ClassicCounter | {gameMode}";
                }
                return Name;
            }
        }

        public string BadgeColor => Region == "NA" ? "#D32F2F" : "#1B6EA8";

        private void Notify(params string[] names)
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var name in names)
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            });
        }
    }
}
