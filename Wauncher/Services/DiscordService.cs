using System.Threading.Tasks;
using Wauncher.Utils;

namespace Wauncher.Services
{
    public class DiscordService : IDiscordService
    {
        public async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                Discord.Init();
            });
        }

        public async Task SetDetailsAsync(string details)
        {
            await Task.Run(() =>
            {
                Discord.SetDetails(details);
            });
        }

        public async Task UpdateAsync()
        {
            await Task.Run(() =>
            {
                Discord.Update();
            });
        }

        public async Task ShutdownAsync()
        {
            await Task.Run(() =>
            {
                Discord.Deinitialize();
            });
        }
    }
}
