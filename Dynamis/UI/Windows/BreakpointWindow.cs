using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dynamis.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dynamis.Interop;
using Dynamis.Interop.Ipfd;
using Dynamis.Interop.Win32;
using Dynamis.Messaging;
using Dynamis.UI.Components;

namespace Dynamis.UI.Windows;

public sealed class BreakpointWindow : IndexedWindow
{
    private const int MaxSnapshotHistorySize = 32;

    private readonly ImGuiComponents _imGuiComponents;
    private readonly ObjectInspector _objectInspector;
    private readonly MessageHub      _messageHub;
    private readonly Breakpoint      _breakpoint;

    private readonly PointerInput _addressInput;

    private bool              _vmEnable;
    private BreakpointFlags   _vmLength;
    private BreakpointFlags   _vmCondition;
    private int               _vmMaximum       = -1;
    private DeduplicationMode _vmDeduplication = DeduplicationMode.ByInstructionPointerAndTypeOfThis;
    private Task?             _vmSyncTask;

    private readonly List<SnapshotRecord>   _vmSnapshots   = [];
    private readonly HashSet<nint>          _vmIps         = [];
    private readonly HashSet<(nint, nint?)> _vmIpsAndTypes = [];

    /// <summary>
    /// Immutable snapshot of <see cref="_vmSnapshots"/> handed to the draw thread. Guarded by
    /// <see cref="_vmSnapshots"/>, and never mutated in place - only ever replaced with a fresh array - so a
    /// reference read under the lock stays valid for as long as the caller needs it outside of the lock.
    /// </summary>
    private SnapshotRecord[] _vmSnapshotsView = [];

    private bool _vmSnapshotsStale;

    public Breakpoint Breakpoint
        => _breakpoint;

    public BreakpointWindow(WindowSystem windowSystem, ImGuiComponents imGuiComponents,
        PointerInputFactory pointerInputFactory, ObjectInspector objectInspector, MessageHub messageHub,
        Breakpoint breakpoint, int index) : base($"{"Dynamis - IPFD Breakpoint".Loc()}##{index}", windowSystem, index)
    {
        _imGuiComponents = imGuiComponents;
        _objectInspector = objectInspector;
        _messageHub = messageHub;
        _breakpoint = breakpoint;

        _addressInput = pointerInputFactory.Create("Address");

        breakpoint.Hit += BreakpointHit;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(768, 432),
            MaximumSize = new(16384, 16384),
        };

        imGuiComponents.AddTitleBarButtons(this);
    }

    public void Configure(nint address, BreakpointFlags condition, BreakpointFlags length, bool enable, int maximumHits, DeduplicationMode deduplicationMode)
    {
        _addressInput.SetValue(address);
        _vmCondition = condition;
        _vmLength = length;
        _vmEnable = enable;
        _vmMaximum = maximumHits;
        _vmDeduplication = deduplicationMode;

        _vmSyncTask = _breakpoint.ModifyAsync(
            address,
            (_vmEnable ? BreakpointFlags.LocalEnable | BreakpointFlags.GlobalEnable : 0) | _vmLength | _vmCondition
        );
    }

    public override void Draw()
    {
        DrawBreakpointEditor();
        DrawBreakpointStatus(_breakpoint.Address, _breakpoint.Flags);
        DrawSnapshots();
    }

    private void DrawBreakpointEditor()
    {
        ImGui.Checkbox("Enable Breakpoint".Loc(), ref _vmEnable);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.CalcTextSize("mmmmm").X + ImGui.GetStyle().FramePadding.X * 2.0f + ImGui.GetStyle().ItemInnerSpacing.X * 2.0f + ImGui.GetFrameHeight() * 2.0f);
        // Read the current value under the lock, run ImGui outside of it, then take the lock again only to
        // write an edited value back. BreakpointHit() takes this same lock from inside the IPFD vectored
        // exception handler, i.e. on whichever game thread tripped the hardware breakpoint - holding it
        // across an ImGui widget stalls that thread for the whole frame, every frame the user keeps the
        // field focused.
        int max;
        lock (this) {
            max = _vmMaximum;
        }

        ImGui.InputInt("Maximum Hits", ref max);
        if (ImGui.IsItemDeactivatedAfterEdit()) {
            var clamped = Math.Clamp(max, -1, ushort.MaxValue);
            lock (this) {
                _vmMaximum = clamped;
            }
        }
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.CalcTextSize("By Address and Type of This").X + ImGui.GetStyle().FramePadding.X * 2.0f + ImGui.GetFrameHeight());
        ImGuiComponents.ComboEnum("Deduplicate", ref _vmDeduplication, GetDeduplication);
        using (ImRaii.PushFont(UiBuilder.MonoFont)) {
            ImGui.SetNextItemWidth(ImGui.CalcTextSize("mmmmmmmmmmmmmmmm").X + ImGui.GetStyle().FramePadding.X * 2.0f);
        }

        _addressInput.Draw();
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.CalcTextSize("Execute").X + ImGui.GetStyle().FramePadding.X * 2.0f + ImGui.GetFrameHeight());
        ImGuiComponents.Combo(
            "Condition", ref _vmCondition,
            [BreakpointFlags.InstructionExecution, BreakpointFlags.DataWrites, BreakpointFlags.DataReadsAndWrites,],
            GetCondition
        );
        ImGui.SameLine();
        ImGui.SetNextItemWidth(ImGui.CalcTextSize("m").X + ImGui.GetStyle().FramePadding.X * 2.0f + ImGui.GetFrameHeight());
        using (ImRaii.Disabled(_vmCondition == BreakpointFlags.InstructionExecution)) {
            ImGuiComponents.Combo(
                "Length", ref _vmLength,
                [
                    BreakpointFlags.LengthOne,
                    BreakpointFlags.LengthTwo,
                    BreakpointFlags.LengthFour,
                    BreakpointFlags.LengthEight,
                ], length => $"{GetLength(length)}"
            );
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(!(_vmSyncTask?.IsCompleted ?? true))) {
            if (ImGui.Button("Apply Configuration".Loc())) {
                _vmSyncTask = _breakpoint.ModifyAsync(
                    _addressInput.GetValue(),
                    (_vmEnable ? BreakpointFlags.LocalEnable | BreakpointFlags.GlobalEnable : 0) | _vmLength
                  | _vmCondition
                );
            }
        }

        if (_vmCondition == BreakpointFlags.DataReadsAndWrites) {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ErrorForeground)) {
                ImGui.TextUnformatted("Read/Write watchpoints in IPFD are known to cause crashes in some circumstances. Use at your own risk!".Loc());
            }
        }
    }

    private static void DrawBreakpointStatus(nint address, BreakpointFlags flags)
    {
        ImGui.TextUnformatted("Current breakpoint status: ".Loc());
        ImGui.SameLine(0.0f, 0.0f);
        if (flags.HasFlag(BreakpointFlags.LocalEnable)) {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.SuccessForeground)) {
                ImGui.TextUnformatted("Enabled".Loc());
            }
        } else {
            using (ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.ErrorForeground)) {
                ImGui.TextUnformatted("Disabled".Loc());
            }
        }

        ImGui.SameLine();
        ImGui.TextUnformatted("Address: ".Loc());
        ImGui.SameLine(0.0f, 0.0f);
        using (ImRaii.PushFont(UiBuilder.MonoFont)) {
            ImGui.TextUnformatted($"0x{address:X}");
        }

        ImGui.SameLine();
        ImGui.TextUnformatted($"Condition: {GetCondition(flags)}");
        if ((flags & BreakpointFlags.DataReadsAndWrites) != BreakpointFlags.InstructionExecution) {
            ImGui.SameLine();
            ImGui.TextUnformatted($"Length: {GetLength(flags)}");
        }
    }

    private void DrawSnapshots()
    {
        using var table = ImRaii.Table("##snapshots", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingFixedFit);
        if (!table) {
            return;
        }

        ImGui.TableSetupColumn("Date/Time",    ImGuiTableColumnFlags.WidthStretch, 0.3f);
        ImGui.TableSetupColumn("Thread ID",    ImGuiTableColumnFlags.WidthStretch, 0.15f);
        ImGui.TableSetupColumn("Address",      ImGuiTableColumnFlags.WidthStretch, 0.2f);
        ImGui.TableSetupColumn("Type of This", ImGuiTableColumnFlags.WidthStretch, 0.2f);
        ImGui.TableSetupColumn("Thread State", ImGuiTableColumnFlags.WidthStretch, 0.15f);
        ImGui.TableHeadersRow();
        // Take the snapshot under the lock, then draw outside of it. ProcessSnapshot() takes this same lock
        // from a thread pool worker to insert a finished record, and BreakpointHit() takes it from the IPFD
        // vectored exception handler to rebuild the deduplication indexes - drawing the whole table inside
        // it makes both wait on a full frame of ImGui, including DrawPointer's symbol resolution and the
        // observer dispatch behind the Inspect button. The records are value copies of a bounded history
        // (at most MaxSnapshotHistorySize entries), and the array is only ever replaced wholesale, so the
        // reference taken under the lock stays valid for the rest of the frame.
        SnapshotRecord[] records;
        lock (_vmSnapshots) {
            if (_vmSnapshotsStale) {
                _vmSnapshotsView = _vmSnapshots.ToArray();
                _vmSnapshotsStale = false;
            }

            records = _vmSnapshotsView;
        }

        var i = 0;
        foreach (var record in records) {
            using var _ = ImRaii.PushId(i++);

            ImGui.TableNextColumn();
            ImGuiComponents.DrawCopyable($"{record.Time}", false);

            ImGui.TableNextColumn();
            ImGuiComponents.DrawCopyable($"{record.ThreadId}", false);

            ImGui.TableNextColumn();
            _imGuiComponents.DrawPointer(
                record.ExceptionAddress, null, null, flags: ImGuiComponents.DrawPointerFlags.RightAligned
            );

            ImGui.TableNextColumn();
            ImGuiComponents.DrawCopyable(
                (record.ClassOfThis?.Name ?? string.Empty).AfterLast("::"), false,
                () => record.ClassOfThis?.Name ?? string.Empty
            );

            ImGui.TableNextColumn();
            if (ImGui.Button("Inspect".Loc())) {
                _messageHub.Publish(new InspectObjectMessage(record.Context));
            }
        }
    }

    private static string GetCondition(BreakpointFlags flags)
        => (flags & BreakpointFlags.DataReadsAndWrites) switch
        {
            BreakpointFlags.InstructionExecution => "Execute",
            BreakpointFlags.DataWrites => "Write",
            BreakpointFlags.DataReadsAndWrites => "R/W",
            BreakpointFlags.IoReadsAndWrites => "I/O",
            _ => throw new Exception($"Unexpected breakpoint condition: {flags & BreakpointFlags.DataReadsAndWrites}"),
        };

    private static byte GetLength(BreakpointFlags flags)
        => (flags & BreakpointFlags.LengthFour) switch
        {
            BreakpointFlags.LengthOne => 1,
            BreakpointFlags.LengthTwo => 2,
            BreakpointFlags.LengthEight => 8,
            BreakpointFlags.LengthFour => 4,
            _ => throw new Exception($"Unexpected breakpoint length: {flags & BreakpointFlags.LengthFour}"),
        };

    private static string GetDeduplication(DeduplicationMode mode)
        => mode switch
        {
            DeduplicationMode.None => "No",
            DeduplicationMode.ByInstructionPointer => "By Address",
            DeduplicationMode.ByInstructionPointerAndTypeOfThis => "By Address and Type of This",
            _ => throw new Exception($"Unexpected deduplication mode: {mode}"),
        };

    public override void OnClose()
    {
        _breakpoint.Hit -= BreakpointHit;
        base.OnClose();
        _breakpoint.Dispose();
    }

    private unsafe void BreakpointHit(object? sender, BreakpointEventArgs e)
    {
        var pCtx = e.ExceptionInfo->ContextRecord;
        var @this = unchecked((nint)e.ExceptionInfo->ContextRecord->Rcx);
        nint? typeOfThis = VirtualMemory.GetProtection(@this).CanRead() && !Ipfd.RequiresSafeRead(@this, pCtx)
            ? *(nint*)@this
            : null;

        bool isIpDuplicate, isIpAndTypeDuplicate;
        lock (_vmIps) {
            isIpDuplicate = _vmIps.Add(e.Address);
            isIpAndTypeDuplicate = _vmIpsAndTypes.Add((e.Address, typeOfThis));
        }

        var isDuplicate = _vmDeduplication switch
        {
            DeduplicationMode.ByInstructionPointer              => isIpDuplicate,
            DeduplicationMode.ByInstructionPointerAndTypeOfThis => isIpAndTypeDuplicate,
            _                                                   => false,
        };

        if (isDuplicate) {
            return;
        }

        int  maximum;
        bool disable;
        lock (this) {
            maximum = _vmMaximum;
            if (_vmMaximum > 0) {
                _vmMaximum--;
            }

            disable = maximum == 1;
        }

        // Outside the lock: DisableAsync() takes the breakpoint's own lock and calls into the native IPFD
        // module, while DrawBreakpointEditor() waits on this lock from the draw thread. maximum == 1 and
        // maximum == 0 are mutually exclusive, so pulling the call out does not reorder it against the
        // early return below.
        if (disable) {
            _vmSyncTask = _breakpoint.DisableAsync();
        }

        if (maximum == 0) {
            if (!isIpDuplicate || !isIpAndTypeDuplicate) {
                // RebuildIndexes() walks _vmSnapshots, which ProcessSnapshot() mutates from a thread pool
                // worker - this call site used to run without that lock held.
                lock (_vmSnapshots) {
                    RebuildIndexes();
                }
            }

            return;
        }

        var time = DateTime.Now;
        var (threadId, context) = _objectInspector.TakeThreadStateSnapshot(in *e.ExceptionInfo->ContextRecord);
        var record = new SnapshotRecord(time, threadId, e.Address, @this, typeOfThis, context);
        ThreadPool.QueueUserWorkItem(ProcessSnapshot, record, false);
    }

    private void ProcessSnapshot(SnapshotRecord record)
    {
        _objectInspector.CompleteSnapshot(record.Context);
        if (record.Context.AssociatedSnapshot is
            {
            } stack) {
            _objectInspector.CompleteSnapshot(stack);
        }

        (record.ClassOfThis, record.DisplacementOfThis) =
            _objectInspector.DetermineClassAndDisplacement(record.This, record.TypeOfThis);

        lock (_vmSnapshots) {
            _vmSnapshots.Insert(0, record);
            if (_vmSnapshots.Count > MaxSnapshotHistorySize) {
                _vmSnapshots.RemoveAt(_vmSnapshots.Count - 1);
                RebuildIndexes();
            }

            _vmSnapshotsStale = true;
        }
    }

    /// <summary>
    /// Rebuilds the deduplication indexes from <see cref="_vmSnapshots"/>. The caller must hold the
    /// <see cref="_vmSnapshots"/> lock - this walks the list, and <see cref="ProcessSnapshot"/> inserts into
    /// it from a thread pool worker. Nesting is always _vmSnapshots then _vmIps, never the other way round.
    /// </summary>
    private void RebuildIndexes()
    {
        lock (_vmIps) {
            _vmIps.Clear();
            _vmIpsAndTypes.Clear();
            foreach (var snapshot in _vmSnapshots) {
                _vmIps.Add(snapshot.ExceptionAddress);
                _vmIpsAndTypes.Add((snapshot.ExceptionAddress, snapshot.TypeOfThis));
            }
        }
    }

    private record struct SnapshotRecord(
        DateTime Time,
        uint ThreadId,
        nint ExceptionAddress,
        nint This,
        nint? TypeOfThis,
        ObjectSnapshot Context)
    {
        public ClassInfo? ClassOfThis;
        public nuint      DisplacementOfThis;
    }

    public enum DeduplicationMode : byte
    {
        None,
        ByInstructionPointer,
        ByInstructionPointerAndTypeOfThis,
    }
}
