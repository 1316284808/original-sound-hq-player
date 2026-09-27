using BassPlayerIpc.Shared;

namespace AudioPlayer.Playback;

public sealed partial class PlaybackEngine
{
    private GaplessSource? _gaplessSource;
    private GaplessRequest? _gaplessRequest;
    private CancellationTokenSource? _gaplessCancellation;
    private SemaphoreSlim? _gaplessOpenGate;
    private Task? _gaplessWork;
    private long _gaplessGeneration, _currentGaplessToken;

    private double EffectivePlaybackRate => _dspSettings is { IsEnabled: true } dsp ? dsp.PlaybackRate : 1;

    private IRenderSource OutputSource(Session session)
    {
        if (session.Kind != RenderKind.Pcm) return session;
        if (_gaplessSource?.Current != session)
        {
            CancelGapless();
            _gaplessSource = new(session);
        }
        return _gaplessSource;
    }

    // Called with the stream lock. Cancellation removes only a future source, never the audible one.
    private void CancelGapless()
    {
        ++_gaplessGeneration;
        _gaplessCancellation?.Cancel();
        _gaplessCancellation = null; // worker owns disposal
        var cancelled = _gaplessSource?.Cancel();
        // Cancel drains an in-flight splice. Adopt before clearing its identity, including a
        // transition that happened after the caller's initial SynchronizeGapless check.
        AdoptGapless();
        cancelled?.Dispose();
        _gaplessRequest = null;
    }

    public void SynchronizeGapless()
    {
        lock (_streamLock) AdoptGapless();
    }

    public (long Token, long Epoch) GetGaplessIdentity()
    {
        lock (_streamLock)
        {
            AdoptGapless();
            return (_currentGaplessToken, _session?.TimelineEpoch ?? 0);
        }
    }

    private void AdoptGapless()
    {
        if (_gaplessSource is not { } source || source.Current == _session || source.CompletedToken == 0) return;
        var old = _session;
        var request = _gaplessRequest;
        SetSession(source.Current);
        if (request != null)
        {
            MusicUrl = request.Path;
            _currentGaplessToken = request.Token;
            _lastProgressSeekId = 0;
            _ipc?.GaplessTransition(request.Token, source.Current.TimelineEpoch);
        }
        _gaplessRequest = null;
        old?.Dispose();
    }

    public void QueueNext(GaplessRequest request)
    {
        lock (_streamLock)
        {
            AdoptGapless();
            if (request.Epoch != 0 && request.Epoch != _session?.TimelineEpoch) return;
            // Keep failed/incompatible attempts too: polling the same plan must not reopen the file every second.
            if (request == _gaplessRequest) return;
            CancelGapless();
            if (!IsPlaying || request.Path.Length == 0 || Volatile.Read(ref _disposed) != 0
                || _dspSettings?.GaplessPlayback != true || _session is not { Kind: RenderKind.Pcm } current
                || _gaplessSource == null || ExperimentalAtmosPassthrough || IsBitstreamActive(request.Path)
                || _currentStream != Guid.Empty || !Path.IsPathFullyQualified(request.Path)) return;
            var cancel = new CancellationTokenSource();
            _gaplessCancellation = cancel;
            _gaplessRequest = request;
            var gate = _gaplessOpenGate ??= new(1, 1);
            var source = _gaplessSource;
            long generation = _gaplessGeneration;
            int dsdRate = DsdPcmFreq, dsdGain = DsdGain, latency = Latency;
            bool surround = ExperimentalSurround51;
            double rate = EffectivePlaybackRate;
            _gaplessWork = Task.Run(async () =>
            {
                Session? next = null;
                bool entered = false;
                try
                {
                    await gate.WaitAsync(cancel.Token).ConfigureAwait(false);
                    entered = true;
                    cancel.Token.ThrowIfCancellationRequested();
                    next = Session.Open(this, request.Path, RenderKind.Pcm, dsdRate, dsdGain, latency,
                        maxChannels: current.Channels <= 2 ? 2 : null, experimentalSurround51: surround,
                        cancellationToken: cancel.Token, playbackRate: rate);
                    lock (_streamLock)
                    {
                        if (next == null || cancel.IsCancellationRequested || generation != _gaplessGeneration
                            || Volatile.Read(ref _disposed) != 0 || _session != current || _gaplessSource != source) return;
                        next.ConfigureDsp(ResolveDsp(_output?.DeviceId));
                        next.Eq.Configure(next.SampleRate, IsEqualizerEnabled, EqGains, EqQ);
                        next.Gain?.SetImmediately(GainVolumeTarget);
                        if (!source.Queue(next, request.Token)) return;
                        next = null; // ownership transferred to the engine
                    }
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
                catch (Exception ex) { Console.WriteLine($"[gapless] preload failed: {ex.Message}"); }
                finally
                {
                    next?.Dispose();
                    if (entered) gate.Release();
                    lock (_streamLock)
                    {
                        if (ReferenceEquals(_gaplessCancellation, cancel)) _gaplessCancellation = null;
                        cancel.Dispose();
                    }
                }
            });
        }
    }
}
