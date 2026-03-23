using System.Threading.Tasks;

namespace Wauncher.Services
{
    public interface IGameService
    {
        Task<bool> LaunchAsync(string? connectTarget = null, string? launchOptions = null);
        Task MonitorAsync();
        bool IsRunning();
        void QueueDeferredConnect(string connectTarget);
        void ClearAdditionalArguments();
        void AddArgument(string argument);
    }
}
