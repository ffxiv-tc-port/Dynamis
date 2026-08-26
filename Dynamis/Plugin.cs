using Dalamud.Game;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dynamis.ClientStructs;
using Dynamis.Configuration;
using Dynamis.Interop;
using Dynamis.Interop.Ipfd;
using Dynamis.Interop.Win32;
using Dynamis.Logging;
using Dynamis.Messaging;
#if WITH_SMA
using Dynamis.PsHost;
#endif
using Dynamis.Resources;
using Dynamis.UI;
using Dynamis.UI.Components;
using Dynamis.UI.ObjectInspectors;
using Dynamis.UI.Windows;
using Dynamis.Utility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dynamis;

public sealed class Plugin : IDalamudPlugin
{
    private readonly CancellationTokenSource _pluginCts = new();
    private readonly Task                    _hostBuilderRunTask;

    public static IPluginLog? Log { get; private set; }

    // TC note: constructor-parameter injection (rather than the newer
    // IDalamudService-marker-interface + IDalamudPluginInterface.GetRequiredService<T>()
    // pair that upstream's "Simplify Dalamud service instantiation" commit switched to)
    // is intentional here, not a stray revert - TC's Dalamud has neither of those, and this
    // is exactly how upstream itself sourced these services before that commit. Reflectively
    // calling IDalamudPluginInterface.Create<T>() for an *interface* type (what a prior TC
    // port attempt of AddDalamudServices() did) doesn't work: Create<T>() is for constructing
    // plugin-defined classes with [PluginService] properties, not for handing back Dalamud's
    // own service singletons - it throws "An eligible ctor with satisfiable services could
    // not be found", which silently faults the whole host (RunAsync() is never awaited
    // outside Dispose(), so nothing ever surfaced this) and meant none of the IHostedServices
    // - including the one registering /dynamis - ever actually started.
    public Plugin(IDalamudPluginInterface pluginInterface, IPluginLog pluginLog,
        ITextureProvider textureProvider, ITextureReadbackProvider readbackProvider,
        ICommandManager commandManager, IChatGui chatGui, IDtrBar dtrBar, ISigScanner sigScanner,
        IGameInteropProvider gameInteropProvider, ITitleScreenMenu titleScreenMenu,
        INotificationManager notificationManager, IFramework framework, IObjectTable objectTable,
        IDataManager dataManager)
    {
        Log = pluginLog;
        Localization.Init(pluginInterface.AssemblyLocation.DirectoryName);

        _hostBuilderRunTask =
            new HostBuilder()
               .UseContentRoot(pluginInterface.ConfigDirectory.FullName)
               .ConfigureLogging(
                    lb =>
                    {
                        lb.ClearProviders();
                        lb.Services.TryAddSingleton<ILoggerProvider, DalamudLoggingProvider>();
                        lb.SetMinimumLevel(LogLevel.Trace);
                    }
                )
               .ConfigureServices(
                    collection =>
                    {
                        collection.AddSingleton(pluginInterface);
                        collection.AddSingleton(pluginLog);
                        collection.AddSingleton(textureProvider);
                        collection.AddSingleton(readbackProvider);
                        collection.AddSingleton(commandManager);
                        collection.AddSingleton(chatGui);
                        collection.AddSingleton(dtrBar);
                        collection.AddSingleton(sigScanner);
                        collection.AddSingleton(gameInteropProvider);
                        collection.AddSingleton(titleScreenMenu);
                        collection.AddSingleton(notificationManager);
                        collection.AddSingleton(framework);
                        collection.AddSingleton(objectTable);
                        collection.AddSingleton(dataManager);

                        collection.AddSingleton(pluginInterface.UiBuilder);

                        collection.AddSingleton(new Dalamud.Localization("Dynamis.Localization.", "", useEmbedded: true));
                        collection.AddSingleton(new WindowSystem("Dynamis"));

                        collection.AddSingleton(
                            new HttpClient()
                            {
                                Timeout = TimeSpan.FromSeconds(5),
                            }
                        );

                        collection.AddSingleton<FileDialogManager>();
                        collection.AddSingleton<MessageHub>();
                        collection.AddSingleton<IpcProvider>();
                        collection.AddSingleton<ResourceProvider>();
                        collection.AddSingleton<ConfigurationContainer>();
                        collection.AddSingleton<DataYamlContainer>();
                        collection.AddSingleton<MemoryHeuristics>();
                        collection.AddSingleton<SymbolApi>();
                        collection.AddSingleton<ModuleAddressResolver>();
                        collection.AddSingleton<AddressIdentifier>();
                        collection.AddSingleton<NanoComProbe>();
                        collection.AddSingleton<ClassRegistry>();
                        collection.AddSingleton<ObjectInspector>();
                        collection.AddSingleton<TextureArraySlicer>();
                        collection.AddSingleton<TextureDumper>();
                        collection.AddSingleton<ImGuiComponents>();
                        collection.AddSingleton<PointerParser>();
                        collection.AddSingleton<PointerInputFactory>();
                        collection.AddSingleton<ContextMenu>();
#if WITH_SMA
                        collection.AddSingleton<BootHelper>();
#endif
                        collection.AddSingleton<DynamicBoxFactory>();
                        collection.AddSingleton<ShortLivedSingleCacheFactory>();

                        collection.AddSingleton<SnapshotViewerFactory>();

                        collection.AddSingleton<ObjectInspectorWindowFactory>();
                        collection.AddSingleton<BreakpointWindowFactory>();
#if WITH_SMA
                        collection.AddSingleton<HostedPsWindowFactory>();
#endif

                        collection.AddImplementationSingletons<IObjectInspector>(typeof(Plugin).Assembly);
                        collection.AddImplementationAliases<IObjectInspector>();
                        collection.AddSingleton<ObjectInspectorDispatcher>();

                        collection.AddImplementationSingletons<ISingletonWindow>(typeof(Plugin).Assembly);

                        collection.AddSingleton<Ipfd>();

                        collection.AddSingleton<Toolbox>();
                        collection.AddSingleton<CommandHandler>();
                        collection.AddSingleton<LaunchButton>();
                        collection.AddSingleton<DevMenuItem>();
                        collection.AddSingleton<WindowManager>();

                        collection.AddLazySingletonAlias<IDalamudLoggingConfiguration, ConfigurationContainer>();

                        collection.AddLazyImplementationAliases<Window>();
                        collection.AddLazyImplementationAliases<ISingletonWindow>();
                        collection.AddSingletonWindowOpeners();
                        collection.AddLazyImplementationAliases<IMessageObserver>();
                        collection.AddLazyImplementationAliases<ObjectInspectorDispatcher>();
                        collection.AddImplementationAliases<IHostedService>();
                    }
                )
               .Build()
               .RunAsync(_pluginCts.Token);
    }

    public void Dispose()
    {
        _pluginCts.Cancel();
        _pluginCts.Dispose();

        // The generic host already applies its own default ~5s shutdown timeout to well-behaved
        // IHostedService implementations, but that's only cooperative - a hosted service (e.g.
        // the PowerShell host under WITH_SMA) that ignores its cancellation token could still
        // hang here indefinitely. Back it with a hard bound so plugin unload can't freeze the
        // game outright.
        if (!_hostBuilderRunTask.Wait(TimeSpan.FromSeconds(10)))
            Log?.Warning("Timed out waiting for Dynamis host to shut down; continuing unload anyway");
    }
}
