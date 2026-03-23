using System;
using System.Collections.Generic;

namespace Wauncher.Services
{
    public class ServiceContainer
    {
        private static readonly Dictionary<Type, object> _services = new();
        private static readonly Dictionary<Type, Func<object>> _factories = new();

        public static void RegisterSingleton<TInterface, TImplementation>()
            where TImplementation : class, TInterface, new()
        {
            _factories[typeof(TInterface)] = () => new TImplementation();
        }

        public static void RegisterSingleton<TInterface>(TInterface instance)
        {
            _services[typeof(TInterface)] = instance;
        }

        public static void RegisterFactory<TInterface>(Func<object> factory)
        {
            _factories[typeof(TInterface)] = factory;
        }

        public static T GetService<T>()
        {
            var type = typeof(T);
            
            if (_services.TryGetValue(type, out var service))
            {
                return (T)service;
            }

            if (_factories.TryGetValue(type, out var factory))
            {
                var instance = factory();
                _services[type] = instance;
                return (T)instance;
            }

            throw new InvalidOperationException($"Service of type {type.Name} is not registered");
        }

        public static void Initialize()
        {
            // Register all services
            RegisterSingleton<IDiscordService, DiscordService>();
            RegisterSingleton<IGameService, GameService>();
            RegisterSingleton<ICarouselService, CarouselService>();
            RegisterSingleton<IUpdateService, UpdateService>();
            RegisterSingleton<IServerService, ServerService>();
            RegisterFactory<IFriendsService>(() => new FriendsService(GetService<IServerService>()));
        }
    }
}
