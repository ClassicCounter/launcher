using Refit;

namespace Launcher.Utils
{
    public class FullGameDownload
    {
        public required string File { get; set; }
        public required string Link { get; set; }
        public required string Hash { get; set; }
    }

    public class FullGameDownloadResponse
    {
        public required List<FullGameDownload> Files { get; set; }
    }

    public interface IGitHub
    {
        [Headers("User-Agent: ClassicCounter Launcher")]
        [Get("/repos/ClassicCounter/launcher/releases/latest")]
        Task<string> GetLatestRelease();
    }

    public interface IClassicCounter
    {
        [Headers("User-Agent: ClassicCounter Launcher")]
        [Get("/patch/get")]
        Task<string> GetPatches();

        [Headers("User-Agent: ClassicCounter Launcher")]
        [Get("/game/get")]
        Task<string> GetFullGameValidate();

        [Headers("User-Agent: ClassicCounter Launcher")]
        [Get("/game/full?steam_id={recentSteamID}")]
        Task<FullGameDownloadResponse> GetFullGameDownload(string recentSteamID);
    }

    public static class Api
    {
        private static RefitSettings _settings = new RefitSettings(new NewtonsoftJsonContentSerializer());
        public static IGitHub GitHub = RestService.For<IGitHub>("https://api.github.com", _settings);
        public static IClassicCounter ClassicCounter = RestService.For<IClassicCounter>("https://classiccounter.cc/api", _settings);
    }
}
