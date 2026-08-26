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

    // TC note: TC's Dalamud has no `Dalamud.IoC.IDalamudService` marker interface (a newer
    // Dalamud convenience that lets every plugin-service interface be discovered generically
    // via reflection over Dalamud.dll's exported types) and no
    // `IDalamudPluginInterface.GetRequiredService<T>()` either - both are newer-Dalamud-only
    // (see the shared skill notes' "recurring old-API-generation compile fixes" list). The
    // old-API equivalent is `pluginInterface.Create<T>()`, which populates a fresh instance's
    // `[PluginService]`-attributed properties - but there's no generic "give me every
    // registered Dalamud service interface" enumeration without that marker interface, so
    // this lists the specific Dalamud service interfaces this repo actually consumes
    // (found via `grep` across the whole tree for constructor-injected `I*` types) instead of
    // scanning for all of them.
    private static readonly Type[] KnownDalamudServiceTypes =
    [
        typeof(IChatGui), typeof(ICommandManager), typeof(IDataManager), typeof(IDtrBar),
        typeof(IFramework), typeof(IGameInteropProvider), typeof(INotificationManager),
        typeof(IObjectTable), typeof(IPluginLog), typeof(ISigScanner), typeof(ITextureProvider),
        typeof(ITextureReadbackProvider),
    ];

    public static void AddDalamudServices(this IServiceCollection collection)
    {
        var createMethod = typeof(IDalamudPluginInterface).GetMethod(nameof(IDalamudPluginInterface.Create))!;
        foreach (var type in KnownDalamudServiceTypes) {
            if (collection.All(t => t.ServiceType != type)) {
                var genericCreate = createMethod.MakeGenericMethod(type);
                collection.AddSingleton(
                    type, provider => genericCreate.Invoke(
                        provider.GetRequiredService<IDalamudPluginInterface>(), [Array.Empty<object>(),]
                    )!
                );
            }
        }
    }

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

    private sealed class SingletonWindowOpener<T>(T window) : IMessageObserver<OpenWindowMessage<T>> where T : Window, ISingletonWindow
    {
        public void HandleMessage(OpenWindowMessage<T> _)
        {
            window.IsOpen = true;
            window.BringToFront();
        }
    }
}
