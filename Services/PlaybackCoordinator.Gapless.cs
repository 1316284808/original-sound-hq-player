using BassPlayerIpc.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using WinUIMusicPlayer.Model;
using static WinUIMusicPlayer.Utils.ToolUtils;

namespace WinUIMusicPlayer.Services;

public sealed partial class PlaybackCoordinator
{
    private sealed record NextPlan(long Token, long Version, Music From, Music Next, long EntryId, long Epoch);
    private NextPlan? _nextPlan;
    private IpcService? _gaplessIpc;
    private DispatcherQueueTimer? _gaplessTimer;
    private INotifyCollectionChanged? _observedQueue;
    private long _nextToken, _lastQueueSend, _lastAcceptedToken;

    // Explicitly started after playback begins; no background work in the constructor.
    private void StartGapless()
    {
        if (_disposed) return;
        if (_gaplessIpc == null)
        {
            _gaplessIpc = ipc;
            _gaplessIpc.NotificationReceived += GaplessNotification;
            state.PropertyChanged += GaplessStateChanged;
            AppSettings.AudioResponseChanged += GaplessPreferencesChanged;
            _gaplessTimer = App.MainWindow.DispatcherQueue.CreateTimer();
            _gaplessTimer.Interval = TimeSpan.FromMilliseconds(500);
            _gaplessTimer.Tick += GaplessTick;
        }
        ObserveQueue();
        UpdateGaplessTimer();
        RefreshGaplessPlan();
    }

    private void UpdateGaplessTimer()
    {
        if (!_disposed && AppSettings.Dsp.GaplessPlayback && state.IsPlaying
            && state.CurrentPlayingMusic is { IsRemote: false } && state.CurrentPlayMode != PlayMode.RepeatOff)
            _gaplessTimer?.Start();
        else _gaplessTimer?.Stop();
    }

    private void GaplessPreferencesChanged(object? sender, EventArgs e)
    {
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            if (_disposed) return;
            UpdateGaplessTimer();
            RefreshGaplessPlan();
        });
    }

    private void ObserveQueue()
    {
        if (ReferenceEquals(_observedQueue, state.CurrentPlayingList)) return;
        if (_observedQueue != null) _observedQueue.CollectionChanged -= GaplessQueueChanged;
        _observedQueue = state.CurrentPlayingList;
        _observedQueue.CollectionChanged += GaplessQueueChanged;
    }

    private void GaplessStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(state.CurrentPlayingList) or nameof(state.CurrentPlayMode) or nameof(state.IsPlaying))
        {
            ObserveQueue();
            UpdateGaplessTimer();
            CancelGaplessPlan();
            RefreshGaplessPlan();
        }
    }

    private void GaplessQueueChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        CancelGaplessPlan();
        RefreshGaplessPlan();
    }

    private void GaplessTick(DispatcherQueueTimer sender, object args) => RefreshGaplessPlan();

    private void RefreshGaplessPlan()
    {
        if (_disposed || !state.CanStartPlayback || _gaplessIpc == null) return;
        if (!state.IsPlaying || !AppSettings.Dsp.GaplessPlayback || state.State.Playback.PendingSelection != null
            || state.CurrentPlayingMusic is not { IsRemote: false, IsPlayable: true } current
            || state.CurrentPlayMode == PlayMode.RepeatOff)
        {
            CancelGaplessPlan();
            return;
        }
        if (!_gaplessIpc.TryGetProgressSnapshot(out var progress) || progress.Epoch <= 0) return;
        if (_nextPlan is { } completed && progress.GaplessToken == completed.Token)
        {
            AcceptGapless(completed.Token);
            return;
        }
        Music? next = null;
        long entry = 0;
        if (state.CurrentPlayMode == PlayMode.SingleLoop)
        {
            next = current;
            entry = state.State.Queue.CurrentEntryId;
        }
        else
        {
            int index = PlaybackCommands.FindCandidateIndex(state.CurrentPlayingList, state.GetCurrentIndex(), 1);
            if (index >= 0)
            {
                next = state.CurrentPlayingList[index];
                entry = state.State.Queue.EntryIdAt(index);
            }
        }
        if (next is not { IsRemote: false, IsPlayable: true }) { CancelGaplessPlan(); return; }
        if (_nextPlan is not { } plan || plan.Epoch != progress.Epoch || plan.EntryId != entry
            || plan.From != current || plan.Next != next || plan.Version != _selectionVersion)
        {
            _nextPlan = new(++_nextToken, _selectionVersion, current, next, entry, progress.Epoch);
            _lastQueueSend = 0;
        }
        // Idempotent refresh also recovers after output reconstruction or DSP changes.
        if (Environment.TickCount64 - _lastQueueSend < 1000) return;
        _lastQueueSend = Environment.TickCount64;
        try { _gaplessIpc.QueueNext(new(_nextPlan.Epoch, _nextPlan.Token, next.Path)); }
        catch (Exception ex) { logger.LogWarning(ex, "无法预载下一曲，保留普通切歌"); }
    }

    private void CancelGaplessPlan()
    {
        if (_nextPlan is not { } plan) return;
        _nextPlan = null;
        _ = CancelGaplessPlanAsync(plan);
    }

    private async Task CancelGaplessPlanAsync(NextPlan plan)
    {
        try
        {
            long committed = await _gaplessIpc!.CancelQueuedNextAsync();
            if (committed == plan.Token) AcceptCommittedGapless(plan);
        }
        catch (Exception ex) { logger.LogWarning(ex, "取消下一曲预载失败"); }
    }

    private void GaplessNotification(MessageTypeId type, ReadOnlyMemory<byte> payload)
    {
        if (type != MessageTypeId.GaplessTransition || payload.Length != 16) return;
        long token = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(payload.Span);
        App.MainWindow.DispatcherQueue.TryEnqueue(() => AcceptGapless(token));
    }

    private void AcceptGapless(long token)
    {
        if (_nextPlan is { } plan && plan.Token == token) AcceptCommittedGapless(plan);
    }

    private void AcceptCommittedGapless(NextPlan plan)
    {
        if (_disposed || !state.CanStartPlayback || plan.Token <= _lastAcceptedToken
            || plan.Version != _selectionVersion || state.CurrentPlayingMusic != plan.From
            || state.State.Playback.PendingSelection != null) return;
        _lastAcceptedToken = plan.Token;
        _nextPlan = null;
        _presentation?.Cancel();
        _presentation?.Dispose();
        _presentation = new CancellationTokenSource();
        var cancellation = _presentation.Token;
        state.State.Queue.SelectEntry(plan.EntryId, plan.Next);
        state.CurrentPlayingMusic = plan.Next;
        state.RemotePlaybackStatus = "";
        try { statistics.StartSession(plan.Next); }
        catch (Exception ex) { logger.LogError(ex, "记录无缝播放统计失败"); }
        state.UILyrics = [];
        state.LoadLyricsToUI(plan.Next);
        state.UpdateProgressTimerUI();
        TrackStarted?.Invoke(plan.Next, cancellation);
        RefreshGaplessPlan();
    }

    private void StopGapless()
    {
        CancelGaplessPlan();
        if (_gaplessTimer != null)
        {
            _gaplessTimer.Stop();
            _gaplessTimer.Tick -= GaplessTick;
        }
        if (_gaplessIpc != null) _gaplessIpc.NotificationReceived -= GaplessNotification;
        state.PropertyChanged -= GaplessStateChanged;
        AppSettings.AudioResponseChanged -= GaplessPreferencesChanged;
        if (_observedQueue != null) _observedQueue.CollectionChanged -= GaplessQueueChanged;
    }
}
