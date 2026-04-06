using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Wauncher.Services
{
    public interface ICarouselService
    {
        Task SetupCarouselAsync();
        Task<List<string>?> LoadCarouselUrlsFromGitHubAsync();
        Task TeardownCarouselAsync();
        bool IsOfflineMode { get; }
        
        // Lazy loading methods
        Task<Bitmap?> LoadImageAsync(int index, string url);
        Task PreloadAdjacentImagesAsync(int currentIndex, List<string> urls);
        Task UnloadDistantImagesAsync(int currentIndex, List<string> urls, int maxCachedCount = 3);
        void ClearImageCache();
    }
}
