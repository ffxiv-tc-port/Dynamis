using System.Numerics;

namespace Dynamis.Utility;

/// <summary>
/// TC note: TC's bundled Dalamud's <see cref="Dalamud.Interface.Colors.ImGuiColors"/> predates
/// the Success/Error/Warning/Info semantic color additions that later Dalamud versions ship
/// (only the original Dalamud*/TankBlue/HealerGreen/DPSRed/Parsed* palette exists there). This
/// compat shim provides the missing names with reasonable equivalents so call sites written
/// against the newer API don't need per-callsite rewriting - files that need these five colors
/// should `using Dynamis.Utility;` instead of `using Dalamud.Interface.Colors;`.
/// </summary>
public static class ImGuiColors
{
    public static readonly Vector4 SuccessForeground = new(0.117f, 1f, 0f, 1f);
    public static readonly Vector4 SuccessBackground = new(0.117f, 0.3f, 0f, 1f);
    public static readonly Vector4 ErrorForeground    = new(1f, 0f, 0f, 1f);
    public static readonly Vector4 WarningForeground  = new(1f, 0.709f, 0f, 1f);
    public static readonly Vector4 InfoForeground     = new(0f, 0.6f, 1f, 1f);
}
