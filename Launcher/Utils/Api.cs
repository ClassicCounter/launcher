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

        [Headers("User-Agent: ClassicCounter Launcher",
            "Accept: application/vnd.github.raw+json")]
        [Get("/repos/ClassicCounter/launcher/contents/dependencies.json")]
        Task<string> GetDependencies();
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
        [Get("/game/full")]
        Task<FullGameDownloadResponse> GetFullGameDownload([Query] string steam_id);
    }

    public static class Api
    {
        private static HttpClientHandler _httpClientHandler = new HttpClientHandler()
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, sslErrors) => true
        };
        private static HttpClient ClassicCounterApiHttpClient = new HttpClient(_httpClientHandler)
        {
            BaseAddress = new Uri("https://classiccounter.cc/api")
        };
        private static RefitSettings _settings = new RefitSettings(new NewtonsoftJsonContentSerializer());
        public static IGitHub GitHub = RestService.For<IGitHub>("https://api.github.com", _settings);
        public static IClassicCounter ClassicCounter = Argument.Exists("--ssl-bypass")
            ? RestService.For<IClassicCounter>(ClassicCounterApiHttpClient, _settings)
            : RestService.For<IClassicCounter>("https://classiccounter.cc/api", _settings); // THIS IS NOT IDEAL, CHANGE THE WORLD OF TRUSTED CERTIFICATES
    }
}
