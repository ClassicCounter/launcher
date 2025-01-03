﻿using Refit;

namespace Launcher.Utils
{
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
        Task<string> GetFullGame();
    }

    public static class Api
    {
        private static RefitSettings _settings = new RefitSettings(new NewtonsoftJsonContentSerializer());
        public static IGitHub GitHub = RestService.For<IGitHub>("https://api.github.com", _settings);
        public static IClassicCounter ClassicCounter = RestService.For<IClassicCounter>("https://classiccounter.cc/api", _settings);
    }
}
