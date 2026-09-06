#if WITH_SMA
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Utility;

namespace Dynamis.UI.PsHost.Output;

public sealed class TextParagraph : IParagraph
{
    private readonly int _id = IParagraph.AllocateId();

    private SeStringBuilder? _builder = new();
    private byte[]?          _value;

    public void Append(string value)
    {
        if (_builder is null) {
            throw new InvalidOperationException("Paragraph has been ended");
        }

        lock (this) {
            _value = null;
            _builder.Append(value);
        }
    }

    public void Append(IEnumerable<Payload> value)
    {
        if (_builder is null) {
            throw new InvalidOperationException("Paragraph has been ended");
        }

        lock (this) {
            _value = null;
            _builder.Append(value);
        }
    }

    public void End()
    {
        if (_builder is null) {
            return;
        }

        lock (this) {
            Update();
            _builder = null;
        }
    }

    public static TextParagraph Create(string value)
    {
        var paragraph = new TextParagraph();
        paragraph.Append(value);
        paragraph.End();
        return paragraph;
    }

    public static TextParagraph Create(IEnumerable<Payload> value)
    {
        var paragraph = new TextParagraph();
        paragraph.Append(value);
        paragraph.End();
        return paragraph;
    }

    private void Update()
        => _value ??= _builder!.Build().EncodeWithNullTerminator();

    public void Draw(ParagraphDrawFlags flags)
    {
        // Encode under the lock (Append/End mutate the builder from the PowerShell threads), then hand the
        // finished buffer to ImGui outside of it. Update() only ever publishes a fresh array and nothing
        // writes into an already published one, so the captured reference stays valid even if a concurrent
        // Append() resets _value in the meantime.
        byte[] value;
        lock (this) {
            Update();
            value = _value!;
        }

        if (flags.HasFlag(ParagraphDrawFlags.CopyOnClick)) {
            var result = ImGuiHelpers.SeStringWrapped(value.AsSpan(..^1), imGuiId: new(_id));
            if (result.Clicked) {
                ImGui.SetClipboardText(SeString.Parse(value.AsSpan(..^1)).TextValue);
            }
        } else {
            ImGuiHelpers.SeStringWrapped(value.AsSpan(..^1));
        }
    }
}
#endif
