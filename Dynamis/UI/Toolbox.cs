using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using Dynamis.Configuration;
using Dynamis.Messaging;
using Dynamis.UI.Windows;

namespace Dynamis.UI;

public class Toolbox(ConfigurationContainer configuration, MessageHub messageHub)
{
    public void Draw(IView view)
    {
        view.Begin();

        if (view.Item("Signature Scanner".Loc())) {
            messageHub.Publish<OpenWindowMessage<SigScannerWindow>>();
        }

        if (view.Item("Object Table".Loc())) {
            messageHub.Publish<OpenWindowMessage<ObjectTableWindow>>();
        }

        if (view.Item("RSV Viewer".Loc())) {
            messageHub.Publish<OpenWindowMessage<RsvWindow>>();
        }

        view.Separator();
        if (view.Item("Object Inspector".Loc())) {
            messageHub.Publish<OpenWindowMessage<ObjectInspectorWindow>>();
        }
#if WITH_SMA
        if (view.Item("Hosted PowerShell".Loc())) {
            messageHub.Publish<OpenWindowMessage<HostedPsWindow>>();
        }
#else
        using (ImRaii.Disabled()) {
            view.Item("Hosted PowerShell".Loc());
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
            using var _ = ImRaii.Tooltip();
            ImGui.TextUnformatted("This Dynamis build does not include the hosted PowerShell.".Loc());
            ImGui.TextUnformatted("To use this, install a build that includes this functionality.".Loc());
        }
#endif

        using (ImRaii.Disabled(!configuration.Configuration.EnableIpfd)) {
            if (view.Item("IPFD Breakpoint".Loc())) {
                messageHub.Publish<OpenWindowMessage<BreakpointWindow>>();
            }
        }

        if (!configuration.Configuration.EnableIpfd && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
            using var _ = ImRaii.Tooltip();
            ImGui.TextUnformatted("The In-Process Faux Debugger is currently disabled.".Loc());
            ImGui.TextUnformatted("To use this, enable this functionality in Dynamis's settings.".Loc());
        }

        view.Separator();

        if (view.Item("Documentation".Loc())) {
            Util.OpenLink("https://github.com/Exter-N/Dynamis/tree/main/docs");
        }
    }

    public interface IView
    {
        void Begin();

        bool Item(string label);

        void Separator();
    }
}
