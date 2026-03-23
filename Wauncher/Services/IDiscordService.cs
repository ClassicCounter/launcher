using System.Threading.Tasks;

namespace Wauncher.Services
{
    public interface IDiscordService
    {
        Task SetDetailsAsync(string details);
        Task UpdateAsync();
        Task InitializeAsync();
        Task ShutdownAsync();
    }
}
