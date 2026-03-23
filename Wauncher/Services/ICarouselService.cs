using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wauncher.Services
{
    public interface ICarouselService
    {
        Task SetupCarouselAsync();
        Task<List<string>?> LoadCarouselUrlsFromGitHubAsync();
        Task TeardownCarouselAsync();
        bool IsOfflineMode { get; }
    }
}
