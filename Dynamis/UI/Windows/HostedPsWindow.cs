#if WITH_SMA
using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dynamis.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dynamis.PsHost;
using Dynamis.UI.PsHost;
using Dynamis.UI.PsHost.Input;
using Dynamis.UI.PsHost.Output;
using Microsoft.Extensions.Logging;
using static Dynamis.Utility.SeStringUtility;

namespace Dynamis.UI.Windows;

public sealed class HostedPsWindow : IndexedWindow, IDisposable
{
    private readonly ILogger                 _logger;
    private readonly BootHelper              _bootHelper;
    private readonly IDalamudPluginInterface _pi;
    private readonly IServiceProvider        _serviceProvider;

    private readonly List<string> _commandHistory = [];

    private readonly List<IParagraph> _output = [];
    private          bool             _scrollToBottom;
    private          bool             _autoScroll  = true;
    private          bool             _copyOnClick = false;

    /// <summary>
    /// Immutable snapshot of <see cref="_output"/> handed to the draw thread. Guarded by
    /// <see cref="_output"/>, and never mutated in place - only ever replaced with a fresh array - so a
    /// reference read under the lock stays valid for as long as the caller needs it outside of the lock.
    /// </summary>
    private IParagraph[] _outputSnapshot = [];

    private bool _outputSnapshotStale;

    private readonly Stack<Section> _currentSections = [];
    private          TextParagraph? _currentParagraph;

    private readonly Dictionary<long, ProgressParagraph>       _activeProgresses = [];
    private readonly List<(IPrompt Prompt, Action OnComplete)> _activePrompts    = [];
    private          bool                                      _focusPrompts;

    private readonly CancellationTokenSource  _closeCts = new();
    private          CancellationTokenSource? _commandCts;
    private          PowerShell?              _currentPowerShell;

    public CancellationToken CurrentCommandCancellationToken
        => _commandCts?.Token ?? CancellationToken.None;

    public HostedPsWindow(ILogger logger, WindowSystem windowSystem, BootHelper bootHelper,
        IDalamudPluginInterface pi, IServiceProvider serviceProvider, ImGuiComponents imGuiComponents,
        int index) : base($"{"Dynamis - Hosted PowerShell".Loc()}##{index}", windowSystem, index, 0)
    {
        _logger = logger;
        _bootHelper = bootHelper;
        _pi = pi;
        _serviceProvider = serviceProvider;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(768, 432),
            MaximumSize = new(16384, 16384),
        };

        imGuiComponents.AddTitleBarButtons(this);

        Task.Factory.StartNew(
                 RunPsSession, CancellationToken.None,
                 TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach
                                                 | TaskCreationOptions.RunContinuationsAsynchronously,
                 TaskScheduler.Default
             )
            .Unwrap();
    }

    private async Task RunPsSession()
    {
        try {
            var ui = new UserInterface(this);
            var context = new HostContext(_serviceProvider);
            var host = new Host(ui, context);
            RunspacePool runspacePool;
            using (_bootHelper.Setup()) {
                runspacePool = RunspaceFactory.CreateRunspacePool(1, 4, _bootHelper.InitialSessionState, host);
            }

            context.RunspacePool = new(runspacePool);
            runspacePool.Open();
            BootHelper.Configure(runspacePool);
            try {
                var profilePath = Path.Combine(_pi.GetPluginConfigDirectory(), "Profile.ps1");
                if (File.Exists(profilePath)) {
                    ExecuteCommand(
                        runspacePool, ui, ps => ps.AddScript($". {profilePath.EscapePsArgument()}"), profilePath
                    );
                }

                while (host.ExitCode is null) {
                    var prompt = GetPrompt(runspacePool);
                    var command = await Prompt(new CommandPrompt(prompt, _commandHistory, runspacePool));
                    ExecuteCommand(runspacePool, ui, command);
                }

                var exitCode = host.ExitCode.Value;
                var exitCodeParagraph = TextParagraph.Create(
                    BuildSeString(
                            $"{(exitCode == 0 ? null : Icon(BitmapFontIcon.Warning))}{(exitCode == 0 ? string.Empty : " ")}{UiForeground(exitCode == 0 ? (ushort)45 : (ushort)16)}Session finished with exit code {exitCode}{UiForegroundOff}"
                        )
                       .Payloads
                );
                lock (_output) {
                    _output.Add(exitCodeParagraph);
                    _outputSnapshotStale = true;
                }
            } finally {
                runspacePool.Close();
                runspacePool.Dispose();
            }
        } catch (OperationCanceledException) {
            // This block intentionally left blank.
        } catch (Exception ex) {
            _logger.LogError(ex, "Error in hosted PowerShell");
        }
    }

    private string GetPrompt(RunspacePool runspacePool)
    {
        using var ps = PowerShell.Create();
        ps.RunspacePool = runspacePool;
        ps.AddCommand("prompt");
        try {
            var prompt = ps.Invoke();
            return prompt.Count > 0 ? prompt[0].ToString() : "PS>";
        } catch (Exception ex) {
            _logger.LogError(ex, "Error while getting PowerShell prompt");
            return "PS>";
        }
    }

    private void ExecuteCommand(RunspacePool runspacePool, UserInterface ui, string command)
        => ExecuteCommand(runspacePool, ui, ps => ps.AddScript(command), command);

    private void ExecuteCommand(RunspacePool runspacePool, UserInterface ui, Action<PowerShell> prepare, string command)
    {
        using var ps = PowerShell.Create();
        ps.RunspacePool = runspacePool;
        using var cts = new CancellationTokenSource();
        _currentPowerShell = ps;
        _commandCts = cts;
        try {
            prepare(ps);
            ps.AddCommand("Out-Default");
            ps.Commands.Commands[^2].MergeMyResults(PipelineResultTypes.Error, PipelineResultTypes.Output);
            BeginSection(command);
            try {
                ps.Invoke();
            } catch (OperationCanceledException) {
                // This block intentionally left blank.
            } catch (Exception ex) {
                _logger.LogError(ex, "Error in PowerShell command {Command}", command);
                ui.WriteErrorLine($"Error in command: {ex}");
            } finally {
                EndSection();
            }
        } finally {
            _commandCts = null;
            _currentPowerShell = null;
        }
    }

    public void BeginSection(string title)
    {
        lock (_currentSections) {
            EndParagraphNoLock();
            Section section;
            lock (_output) {
                section = new(title, _output.Count, false);
                _output.Add(section);
                _outputSnapshotStale = true;
            }

            _currentSections.Clear();
            _currentSections.Push(section);
            _scrollToBottom = true;
        }
    }

    public void EndSection()
    {
        lock (_currentSections) {
            EndParagraphNoLock();
            _currentSections.Clear();
        }
    }

    public void BeginNestedSection(string title)
    {
        lock (_currentSections) {
            EndParagraphNoLock();
            var section = _currentSections.Peek().AddSubSection(title);
            _currentSections.Push(section);
            _scrollToBottom = true;
        }
    }

    public void EndNestedSection()
    {
        lock (_currentSections) {
            if (_currentSections.Count < 2) {
                throw new InvalidOperationException("Not in a nested section");
            }

            EndParagraphNoLock();
            _currentSections.Pop();
        }
    }

    public void AddParagraph(IParagraph paragraph)
    {
        lock (_currentSections) {
            EndParagraphNoLock();
            _currentSections.Peek().Add(paragraph);
            _scrollToBottom = true;
        }
    }

    public void Write(string value)
    {
        lock (_currentSections) {
            if (_currentParagraph is null) {
                _currentParagraph = new();
                _currentSections.Peek().Add(_currentParagraph);
            }

            _currentParagraph.Append(value);
            _scrollToBottom = true;
        }
    }

    public void WriteInterpolated(ref SeStringInterpolatedStringHandler handler)
    {
        var value = BuildSeString(ref handler);

        lock (_currentSections) {
            if (_currentParagraph is null) {
                _currentParagraph = new();
                _currentSections.Peek().Add(_currentParagraph);
            }

            _currentParagraph.Append(value.Payloads);
            _scrollToBottom = true;
        }
    }

    public void EndParagraph()
    {
        lock (_currentSections) {
            EndParagraphNoLock();
        }
    }

    private void EndParagraphNoLock()
    {
        if (_currentParagraph is not null) {
            _currentParagraph.End();
            _currentParagraph = null;
        }
    }

    public void WriteProgress(long sourceId, ProgressRecord record)
    {
        lock (_activeProgresses) {
            if (!_activeProgresses.TryGetValue(sourceId, out var paragraph)) {
                paragraph = new();
                _activeProgresses.Add(sourceId, paragraph);
                AddParagraph(paragraph);
            }

            paragraph.Update(record);
            if (record.RecordType is ProgressRecordType.Completed) {
                _activeProgresses.Remove(sourceId);
            }
        }
    }

    public Task<T> Prompt<T>(IPrompt<T> prompt, bool inline = false, CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = false;
        CancellationTokenRegistration registration = default;
        CancellationTokenRegistration closeRegistration = default;
        var onComplete = new Action(
            [SuppressMessage("ReSharper", "AccessToModifiedClosure")]() =>
            {
                tcs.SetResult(prompt.Result);
                registration.Dispose();
                closeRegistration.Dispose();
            }
        );
        var cancel = (Action)tcs.SetCanceled + prompt.Cancel;
        if (inline) {
            lock (_currentSections) {
                var sameLine = _currentParagraph is not null;
                EndParagraphNoLock();

                _currentSections.Peek().Add(new InlinePrompt(prompt, onComplete, sameLine, _activePrompts.Count == 0));
                _scrollToBottom = true;
            }
        } else {
            lock (_activePrompts) {
                _activePrompts.Add((prompt, onComplete));
                _focusPrompts |= _activePrompts.Count == 1;
            }

            cancel += () =>
            {
                lock (_activePrompts) {
                    _activePrompts.Remove((prompt, onComplete));
                }
            };
        }

        cancel += [SuppressMessage("ReSharper", "AccessToModifiedClosure")]() =>
        {
            canceled = true;
            registration.Dispose();
            closeRegistration.Dispose();
        };

        registration = cancellationToken.Register(cancel);
        if (!canceled) {
            closeRegistration = _closeCts.Token.Register(cancel);
        }

        return tcs.Task;
    }

    public override void Draw()
    {
        DrawToolbar();
        using var _ = ImRaii.PushFont(UiBuilder.MonoFont);

        // Snapshot the queued prompts under the lock and run every ImGui call outside of it. The PowerShell
        // threads take this same lock to queue a prompt, and drawing a prompt can take arbitrarily long:
        // CommandPrompt's tab completion runs a pipeline in the runspace pool, and a pool worker may answer
        // it by asking the host for another prompt, i.e. by taking this very lock.
        (IPrompt Prompt, Action OnComplete)[] prompts;
        lock (_activePrompts) {
            prompts = _activePrompts.ToArray();
        }

        var size = ImGui.GetContentRegionAvail();
        size.Y -= MeasureInputHeight(prompts);
        DrawOutput(size);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ChildWindows) && ImGui.GetIO().MouseWheel > 0.0f) {
            _autoScroll = false;
        }

        DrawInput(prompts);
    }

    private void DrawToolbar()
    {
        using (ImRaii.Disabled(_currentPowerShell is null)) {
            if (ImGuiComponents.NormalizedIconTextButton(FontAwesomeIcon.Stop, "Abort current command")) {
                _commandCts?.Cancel();
                _currentPowerShell?.StopAsync(null, null);
                try {
                    AddParagraph(
                        TextParagraph.Create(
                            BuildSeString(
                                    $"{BitmapFontIcon.Warning}{UiForeground(16)} Command aborted{UiForegroundOff}"
                                )
                               .Payloads
                        )
                    );
                } catch (InvalidOperationException) {
                    // No command was running - ignore.
                }
            }
        }

        ImGui.SameLine();
        if (ImGuiComponents.NormalizedIconTextButton(FontAwesomeIcon.Trash, "Clear output history")) {
            lock (_currentSections) {
                lock (_output) {
                    if (_currentSections.Count > 0) {
                        _output.RemoveRange(0, _output.Count - 1);
                    } else {
                        _output.Clear();
                    }

                    _outputSnapshotStale = true;
                }
            }
        }

        ImGui.SameLine();
        ImGui.Checkbox("Copy mode".Loc(), ref _copyOnClick);
        ImGui.SameLine(0.0f, ImGui.GetStyle().ItemInnerSpacing.X);
        ImGuiComponents.NormalizedIcon(FontAwesomeIcon.InfoCircle, ImGuiColors.InfoForeground.ToUInt32());

        if (ImGui.IsItemHovered()) {
            using var _ = ImRaii.Tooltip();
            ImGui.TextUnformatted("Click an output line to copy it (as plain text) to your clipboard.".Loc());
        }

        ImGui.SameLine();
        ImGui.Checkbox("Automatically scroll to latest output".Loc(), ref _autoScroll);
    }

    private void DrawOutput(Vector2 size)
    {
        using var outputChild = ImRaii.Child("##Output", size, true);
        if (!outputChild) {
            return;
        }

        ParagraphDrawFlags flags = 0;
        if (_copyOnClick) {
            flags |= ParagraphDrawFlags.CopyOnClick;
        }

        // Same shape as Section.DrawChildren: rebuild the snapshot under the lock when it went stale,
        // carry only the array reference out, and draw with no lock held.
        IParagraph[] output;
        lock (_output) {
            if (_outputSnapshotStale) {
                _outputSnapshot = _output.ToArray();
                _outputSnapshotStale = false;
            }

            output = _outputSnapshot;
        }

        foreach (var section in output) {
            section.Draw(flags);
        }

        if (_scrollToBottom && _autoScroll) {
            ImGui.SetScrollHereY();
        }

        _scrollToBottom = false;
    }

    private static float MeasureInputHeight(ReadOnlySpan<(IPrompt Prompt, Action OnComplete)> prompts)
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var height = 0.0f;
        foreach (var entry in prompts) {
            var promptHeight = entry.Prompt.Height;
            if (promptHeight > 0.0f) {
                height += promptHeight + spacing;
            }
        }

        return height;
    }

    private void DrawInput(ReadOnlySpan<(IPrompt Prompt, Action OnComplete)> prompts)
    {
        foreach (var entry in prompts) {
            if (!entry.Prompt.Draw(ref _focusPrompts)) {
                continue;
            }

            // Only whoever actually takes the entry out of the list runs its completion, so a prompt that
            // was cancelled while this frame was drawing is not completed a second time.
            bool removed;
            lock (_activePrompts) {
                removed = _activePrompts.Remove(entry);
            }

            if (removed) {
                entry.OnComplete();
            }
        }
    }

    public override void OnClose()
    {
        _closeCts.Cancel();
        _commandCts?.Cancel();
        _currentPowerShell?.StopAsync(null, null);
        base.OnClose();
    }

    public void Dispose()
    {
        _closeCts.Cancel();
        _commandCts?.Cancel();
        _currentPowerShell?.StopAsync(null, null);
    }
}
#endif
