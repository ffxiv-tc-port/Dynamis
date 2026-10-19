using Dalamud.Interface.Windowing;

namespace Dynamis.Messaging;

/// <summary>開著就關、關著就開（只對單例視窗有效）。</summary>
public record ToggleWindowMessage<T> where T : Window;
