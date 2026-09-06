#if WITH_SMA
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace Dynamis.UI.PsHost.Output;

public sealed class Section(string title, int index, bool nested) : IParagraph
{
    private readonly List<IParagraph> _children = [];

    /// <summary>
    /// Immutable snapshot of <see cref="_children"/> handed to the draw thread. Guarded by
    /// <see cref="_children"/>, and never mutated in place - only ever replaced with a fresh array - so a
    /// reference read under the lock stays valid for as long as the caller needs it outside of the lock.
    /// </summary>
    private IParagraph[] _snapshot = [];

    private bool _snapshotStale;

    public void Add(IParagraph child)
    {
        lock (_children) {
            _children.Add(child);
            _snapshotStale = true;
        }
    }

    public Section AddSubSection(string subTitle)
    {
        lock (_children) {
            var section = new Section(subTitle, _children.Count, true);
            _children.Add(section);
            _snapshotStale = true;
            return section;
        }
    }

    public void Draw(ParagraphDrawFlags flags)
    {
        if (nested) {
            using var node = ImRaii.TreeNode($"{title}###{index}", ImGuiTreeNodeFlags.DefaultOpen);
            if (node) {
                DrawChildren(flags);
            }
        } else {
            if (!ImGui.CollapsingHeader($"{title}###{index}", ImGuiTreeNodeFlags.DefaultOpen)) {
                return;
            }

            using (ImRaii.PushId(index)) {
                DrawChildren(flags);
            }
        }
    }

    private void DrawChildren(ParagraphDrawFlags flags)
    {
        // Take the snapshot under the lock, then draw outside of it. The PowerShell pipeline threads take
        // this same lock to append output, so drawing inside it makes each thread wait on the other.
        IParagraph[] children;
        lock (_children) {
            if (_snapshotStale) {
                _snapshot = _children.ToArray();
                _snapshotStale = false;
            }

            children = _snapshot;
        }

        foreach (var child in children) {
            child.Draw(flags);
        }
    }
}
#endif
