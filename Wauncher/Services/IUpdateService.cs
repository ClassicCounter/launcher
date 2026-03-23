using System.Threading.Tasks;

namespace Wauncher.Services
{
    public interface IUpdateService
    {
        Task<bool> CheckForUpdatesAsync();
        Task<bool> InstallGameFromCdnAsync();
        Task<bool> ValidateGameFilesAsync();
        bool IsUpdateAvailable { get; }
        bool IsNeedingInstall { get; }
        bool IsCheckingUpdates { get; }
        bool IsUpdating { get; }
        bool IsInstalling { get; }
        double UpdateProgress { get; }
        string UpdateStatus { get; }
        string UpdateStatusFile { get; }
        string UpdateStatusSpeed { get; }
        bool UpdateIndeterminate { get; }
    }
}
