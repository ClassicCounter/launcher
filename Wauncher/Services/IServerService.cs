using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Wauncher.ViewModels;

namespace Wauncher.Services
{
    public interface IServerService
    {
        ObservableCollection<ServerInfo> Servers { get; }
        void Start();
        Task RefreshServersAsync();
        Task RefreshServersSafeAsync();
        bool IsOfflineMode { get; }
    }
}
