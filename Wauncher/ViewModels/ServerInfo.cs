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
                Notify(nameof(Map), nameof(MapDisplay));
            }
        }

        public bool IsNone => string.IsNullOrEmpty(IpPort);

        public string PlayerCount => IsNone ? "" : $"{Players}/{MaxPlayers}";
        public string DotColor => IsNone ? "Transparent" : (IsOnline ? "#4CAF50" : "#F44336");
        public string NameColor => IsNone ? "#66FFFFFF" : "White";
        public string MapDisplay => (!IsNone && !string.IsNullOrEmpty(Map)) ? Map : "";

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
