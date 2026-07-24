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

    // TC note: TC's Dalamud has no `Dalamud.IoC.IDalamudService` marker interface and no
    // `IDalamudPluginInterface.GetRequiredService<T>()` either - both are newer-Dalamud-only
    // (see the shared skill notes' "recurring old-API-generation compile fixes" list). A
    // prior TC port attempt tried working around this with
    // `IDalamudPluginInterface.Create<T>()`, reasoning it as the old-API equivalent - but
    // `Create<T>()` only populates a fresh *plugin-defined class's* `[PluginService]`
    // properties, it can't construct one of Dalamud's own service interfaces directly, and
    // silently faulted the whole DI host at startup. The actual old-API-generation way to get
    // these is what `Plugin`'s constructor now does: take each one as a normal constructor
    // parameter (same mechanism every `[PluginService]`-attributed plugin here relies on) and
    // `collection.AddSingleton(...)` the already-resolved instance - see upstream's own
    // pre-"Simplify Dalamud service instantiation" (de9c735) `Plugin.cs` for the original of
    // this exact pattern.

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
