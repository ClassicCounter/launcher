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
                try
                {
                    Discord.Init();
                }
                catch
                {
                    // Discord integration is optional.
                }
            });
        }

        public async Task SetDetailsAsync(string details)
        {
            await Task.Run(() =>
            {
                try
                {
                    Discord.SetDetails(details);
                }
                catch
                {
                    // Discord integration is optional.
                }
            });
        }

        public async Task UpdateAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    Discord.Update();
                }
                catch
                {
                    // Discord integration is optional.
                }
            });
        }

        public async Task ShutdownAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    Discord.Deinitialize();
                }
                catch
                {
                    // Discord integration is optional.
                }
            });
        }
    }
}
