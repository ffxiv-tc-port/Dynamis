using ImGuiNET;
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

        if (view.Item("特徵碼掃描器")) {
            messageHub.Publish<OpenWindowMessage<SigScannerWindow>>();
        }

        if (view.Item("物件表")) {
            messageHub.Publish<OpenWindowMessage<ObjectTableWindow>>();
        }

        if (view.Item("RSV 檢視器")) {
            messageHub.Publish<OpenWindowMessage<RsvWindow>>();
        }

        view.Separator();
        if (view.Item("物件檢查器")) {
            messageHub.Publish<OpenWindowMessage<ObjectInspectorWindow>>();
        }
#if WITH_SMA
        if (view.Item("寄宿式 PowerShell")) {
            messageHub.Publish<OpenWindowMessage<HostedPsWindow>>();
        }
#else
        using (ImRaii.Disabled()) {
            view.Item("寄宿式 PowerShell");
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
            using var _ = ImRaii.Tooltip();
            ImGui.TextUnformatted("此 Dynamis 版本未包含寄宿式 PowerShell 功能。");
            ImGui.TextUnformatted("若要使用此功能，請安裝包含此功能的版本。");
        }
#endif

        using (ImRaii.Disabled(!configuration.Configuration.EnableIpfd)) {
            if (view.Item("IPFD 中斷點")) {
                messageHub.Publish<OpenWindowMessage<BreakpointWindow>>();
            }
        }

        if (!configuration.Configuration.EnableIpfd && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) {
            using var _ = ImRaii.Tooltip();
            ImGui.TextUnformatted("行程內偽偵錯器（IPFD）目前已停用。");
            ImGui.TextUnformatted("若要使用此功能，請在 Dynamis 的設定中啟用。");
        }

        view.Separator();

        if (view.Item("說明文件")) {
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
