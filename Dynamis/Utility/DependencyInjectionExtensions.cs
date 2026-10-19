using System.Reflection;
using Dalamud.Game;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dynamis.Messaging;
using Dynamis.UI.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace Dynamis.Utility;

internal static class DependencyInjectionExtensions
{
    public static IServiceCollection AddSingletonAlias<TAlias, TImplementation>(this IServiceCollection collection) where TAlias : class where TImplementation : TAlias
        => collection.AddSingleton<TAlias>(s => s.GetRequiredService<TImplementation>());

    public static IServiceCollection AddLazySingletonAlias<TAlias, TImplementation>(this IServiceCollection collection) where TAlias : class where TImplementation : TAlias
        => collection.AddSingleton<Lazy<TAlias>>(s => new(() => s.GetRequiredService<TImplementation>()));

    public static void AddImplementationAliases<T>(this IServiceCollection collection) where T : class
    {
        var aliases = (
            from descriptor in collection
            let type = descriptor.ServiceType
            where descriptor.ImplementationType == type && typeof(T).IsAssignableFrom(type)
            select new ServiceDescriptor(typeof(T), MakeServiceFactory<T>(type), descriptor.Lifetime)
        ).ToList();

        foreach (var alias in aliases) {
            collection.Add(alias);
        }
    }

    public static void AddLazyImplementationAliases<T>(this IServiceCollection collection) where T : class
    {
        var aliases = (
            from descriptor in collection
            let type = descriptor.ServiceType
            where descriptor.ImplementationType == type && typeof(T).IsAssignableFrom(type)
            select new ServiceDescriptor(typeof(Lazy<T>), MakeLazyServiceFactory<T>(type), descriptor.Lifetime)
        ).ToList();

        foreach (var alias in aliases) {
            collection.Add(alias);
        }
    }

    public static void AddImplementationSingletons<T>(this IServiceCollection collection, Assembly assembly) where T : class
    {
        var t = typeof(T);
        foreach (var type in assembly.GetTypes()
                                     .Where(
                                          type => type.IsClass && !type.IsAbstract && !type.IsInterface
                                               && !type.IsGenericTypeDefinition && t.IsAssignableFrom(type)
                                      )) {
            collection.AddSingleton(type);
        }
    }

    public static void AddSingletonWindowOpeners(this IServiceCollection collection)
    {
        var openers = (
            from descriptor in collection
            let type = descriptor.ServiceType
            where descriptor.ImplementationType == type && typeof(Window).IsAssignableFrom(type)
                                                        && typeof(ISingletonWindow).IsAssignableFrom(type)
            let swoType = typeof(SingletonWindowOpener<>).MakeGenericType(type)
            select new ServiceDescriptor(swoType, swoType, descriptor.Lifetime)
        ).ToList();

        foreach (var opener in openers) {
            collection.Add(opener);
        }
    }

    // TC note: there is deliberately no AddDalamudServices() here. TC's Dalamud has no
    // `IDalamudPluginInterface.GetRequiredService<T>()`, and `IDalamudPluginInterface.Create<T>()` cannot hand back
    // Dalamud's own service interfaces (it only fills a plugin-defined class's `[PluginService]` properties).
    // `Plugin`'s constructor takes each service as a normal constructor parameter and registers the
    // already-resolved instance with `collection.AddSingleton(...)` instead.

    private static Func<IServiceProvider, T> MakeServiceFactory<T>(Type t) where T : notnull
        => (Func<IServiceProvider, T>)typeof(DependencyInjectionExtensions).GetMethod(nameof(ServiceFactory), BindingFlags.NonPublic | BindingFlags.Static)!
           .MakeGenericMethod(t)
           .CreateDelegate(typeof(Func<,>).MakeGenericType(typeof(IServiceProvider), t));

    private static T ServiceFactory<T>(IServiceProvider provider) where T : notnull
        => provider.GetRequiredService<T>();

    private static Func<IServiceProvider, Lazy<T>> MakeLazyServiceFactory<T>(Type t) where T : notnull
        => typeof(DependencyInjectionExtensions).GetMethod(nameof(LazyServiceFactory), BindingFlags.NonPublic | BindingFlags.Static)!
           .MakeGenericMethod(typeof(T), t)
           .CreateDelegate<Func<IServiceProvider, Lazy<T>>>();

    private static Lazy<TService> LazyServiceFactory<TService, TImplementation>(IServiceProvider provider) where TService : notnull where TImplementation : TService
        => new(() => provider.GetRequiredService<TImplementation>());

    private sealed class SingletonWindowOpener<T>(T window) : IMessageObserver<OpenWindowMessage<T>>, IMessageObserver<ToggleWindowMessage<T>> where T : Window, ISingletonWindow
    {
        public void HandleMessage(OpenWindowMessage<T> _)
        {
            window.IsOpen = true;
            window.BringToFront();
        }

        public void HandleMessage(ToggleWindowMessage<T> _)
        {
            if (window.IsOpen) {
                window.IsOpen = false;
                return;
            }

            window.IsOpen = true;
            window.BringToFront();
        }
    }
}
