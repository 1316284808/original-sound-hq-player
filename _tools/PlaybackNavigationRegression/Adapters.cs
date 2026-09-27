using WinUIMusicPlayer.Model;

namespace WinUIMusicPlayer
{
#if REAL_WINUI
    public static class App { public static Microsoft.UI.Xaml.Window MainWindow { get; set; } = null!; public static IServiceProvider Services = null!; }
#else
    public static class App { public static Window MainWindow { get; } = new(); public static IServiceProvider Services = null!; }
    public sealed class Window
    {
        public UiQueue DispatcherQueue { get; } = new();
        public ContentStub Content { get; } = new();
    }
    public sealed class ContentStub { public object XamlRoot { get; } = new(); }
    public sealed class UiQueue
    {
        private SynchronizationContext _context = null!;
        private int _thread;
        public void Attach() { _context = SynchronizationContext.Current!; _thread = Environment.CurrentManagedThreadId; }
        public Microsoft.UI.Dispatching.DispatcherQueueTimer CreateTimer() => new(this);
        public bool HasThreadAccess => Environment.CurrentManagedThreadId == _thread;
        public void Post(Action action) => _context.Post(_ => action(), null);
        public bool TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueueHandler action) { Post(() => action()); return true; }
    }
#endif
}
#if !REAL_WINUI
namespace Microsoft.UI.Dispatching
{
    public delegate void DispatcherQueueHandler();
    public sealed class DispatcherQueueTimer(WinUIMusicPlayer.UiQueue queue)
    {
        private System.Threading.Timer? _timer;
        public TimeSpan Interval { get; set; }
        public event Action<DispatcherQueueTimer, object>? Tick;
        public void Start() => _timer ??= new System.Threading.Timer(_ => queue.Post(() => { if (_timer != null) Tick?.Invoke(this, new()); }), null, Interval, Interval);
        public void Stop() { _timer?.Dispose(); _timer = null; }
    }
}
#endif
namespace CommunityToolkit.WinUI
{
    public static class Dispatch
    {
#if REAL_WINUI
        public static Task EnqueueAsync(this Microsoft.UI.Dispatching.DispatcherQueue queue, Action action)
#else
        public static Task EnqueueAsync(this WinUIMusicPlayer.UiQueue queue, Action action)
#endif
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            queue.TryEnqueue(() => { try { action(); done.SetResult(); } catch (Exception ex) { done.SetException(ex); } });
            return done.Task;
        }
    }
}
namespace WinUIMusicPlayer.Model
{
    public sealed class Music(int id, int source = 0) : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public string Path => source == 0 ? System.IO.Path.GetFullPath($"track-{id}.wav") : $"webdav://{source}/dav/tone.flac";
        public TimeSpan Duration { get; set; }
        public int Id => id;
        public int SourceId => source;
        public bool IsRemote => source != 0;
        public bool IsRemoteOffline
        {
            get;
            set { if (SetProperty(ref field, value)) OnPropertyChanged(nameof(IsPlayable)); }
        }
        public bool IsRemoteCached
        {
            get;
            set { if (SetProperty(ref field, value)) OnPropertyChanged(nameof(IsPlayable)); }
        }
        public bool IsPlayable => IsRemoteCached || !IsRemoteOffline;
    }
    public sealed record WebDavSource(int Id)
    {
        public bool Enabled => true;
        public string Name => "Test source";
        public string BaseUri => "http://127.0.0.1/dav/";
    }
    public sealed class WebDavCacheSettings { public bool Enabled { get; set; } public int LimitGiB { get; set; } = 10; }
    public static class AppSettings
    {
        public static string MusicCoverCache { get; set; } = Path.GetTempPath();
        public static event EventHandler? AudioResponseChanged;
        public static BassPlayerIpc.Shared.DspSettings Dsp { get; set { field = value; AudioResponseChanged?.Invoke(null, EventArgs.Empty); } } = new();
    }
}
namespace WinUIMusicPlayer.Utils { public static class ToolUtils { public enum PlayMode { SingleLoop, ListLoop, RandomLoop, RepeatOff } public static string GetString(string key) => key; } }
namespace WinUIMusicPlayer.Helper
{
    public static class DialogHelper { public static Task<bool> ShowConfirmAsync(object root, string titleKey) => Task.FromResult(true); }
}
namespace WinUIMusicPlayer.Services.WebDav
{
#if !REAL_REMOTE
    public static class WebDavCachePaths { public static string Root(string path) => Path.Combine(path, "WebDav"); }
    public sealed class RemoteAudioCache { public long GetSize() => 0; public void Clear() { } }
#endif
}
namespace WinUIMusicPlayer.ViewModel
{
    public sealed class AppViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public bool CanPublishState => CanStartPlayback;
        public bool IsPlaybackEngineReady => true;
        public bool CanStartPlayback { get; set; } = true;
        public System.Collections.ObjectModel.ObservableCollection<Music> CurrentPlayingList { get; set; } = [];
        public WinUIMusicPlayer.Utils.ToolUtils.PlayMode CurrentPlayMode { get; set => SetProperty(ref field, value); } = WinUIMusicPlayer.Utils.ToolUtils.PlayMode.ListLoop;
        public Music? CurrentPlayingMusic { get; set => SetProperty(ref field, value); }
        public Music? SelectedPlaybackMusic => State.Playback.PendingSelection?.Music ?? CurrentPlayingMusic;
        public int GetSelectedPlaybackIndex() => State.Playback.PendingSelection is { } pending ? (int)pending.EntryId - 1 : GetCurrentIndex();
        public int GetCurrentIndex() => CurrentPlayingList.IndexOf(CurrentPlayingMusic!);
        public bool IsPlaying { get; set => SetProperty(ref field, value); }
        public double ProgressSlider { get; set; }
        public double ProgressSliderMax { get; set; }
        public string RemotePlaybackStatus { get; set; } = "";
        public List<string> UILyrics { get; set; } = [];
        public AppState State { get; } = new();
        public System.Collections.ObjectModel.ObservableCollection<Music> SongsSource => CurrentPlayingList;
        public string MusicCoverCache => WinUIMusicPlayer.Model.AppSettings.MusicCoverCache;
        public void RefreshDataSource() { }
        public void StartProgressTimer() { }
        public void BeginProgressSeek(long ms, long id) { }
        public void CancelProgressSeek(long id) { }
        public void StopProgressTimer() { }
        public void LoadLyricsToUI(Music music) { }
        public void UpdateProgressTimerUI() { }
    }
    public sealed class ShellState { public bool InfoBarIsOpen { get; set; } public string InfoBarTitle { get; set; } = ""; public string InfoBarMessage { get; set; } = ""; }
    public sealed class AppState
    {
        public ShellState Shell { get; } = new();
        public WinUIMusicPlayer.State.PlaybackState Playback { get; } = new();
        public QueueState Queue { get; } = new();
        public BrowseState Browse { get; } = new();
        public PreferencesState Preferences { get; } = new();
    }
    public sealed class BrowseState { public int SourceFilterId { get; set; } }
    public sealed class PreferencesState : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public string MusicCoverCache { get; set => SetProperty(ref field, value); } = Path.GetTempPath();
    }
    public sealed class QueueState
    {
        public Func<Music, int> FindIndex { get; set; } = _ => -1;
        public int IndexOf(Music music) => FindIndex(music);
        public long CurrentEntryId { get; set; }
        public long EntryIdAt(int index) => index < 0 ? 0 : index + 1;
        public void SelectEntry(long id, Music music) => CurrentEntryId = id != 0 ? id : EntryIdAt(IndexOf(music));
    }
}
#if !REAL_REMOTE
namespace WinUIMusicPlayer.Services.WebDav
{
    public sealed class WebDavException(string code) : IOException { public string Code => code; }
}
#endif
namespace WinUIMusicPlayer.Services
{
#if !REAL_REMOTE
    public sealed class MusicDatabaseService
    {
        public Task<List<WebDavSource>> GetWebDavSourcesAsync() => Task.FromResult(new List<WebDavSource> { new(10) });
        public Task<Dictionary<int, int>> GetWebDavSourceTrackCountsAsync() => Task.FromResult(new Dictionary<int, int>());
        public Task<int> GetWebDavSourceTrackCountAsync(int sourceId) => Task.FromResult(0);
        public Task<WebDavCacheSettings> GetWebDavCacheSettingsAsync() => Task.FromResult(new WebDavCacheSettings());
    }
    public sealed class LibraryQueries { public void SetSourceFilter(int sourceId) { } }
    public sealed record WebDavScanStatus(int SourceId, string Phase, int Found, int Tagged, string? Error = null);
#endif
    public sealed class BassPlayerCommandService
    {
        public WinUIMusicPlayer.ViewModel.AppViewModel? State;
        public List<Music> Played { get; } = [];
        public int Ends;
        public void PlayMusic(Music music)
        {
            if (!App.MainWindow.DispatcherQueue.HasThreadAccess) throw new Exception("play called outside UI thread");
            Played.Add(music);
        }
        public void MusicEnd(bool preserveInterruptedProgress = false)
        {
            Ends++;
            if (!preserveInterruptedProgress && State is not null) State.ProgressSlider = 0;
        }
        public void PlayNextTrack() { }
        public Task PlayButton() => Task.CompletedTask;
        public void ChangeWaveChannelTime(long ms) { }
    }
    public sealed class IpcService
    {
        public BassPlayerIpc.Shared.ProgressSnapshot? Progress;
        public List<BassPlayerIpc.Shared.GaplessRequest> Queued { get; } = [];
        public event Action<BassPlayerIpc.Shared.MessageTypeId, ReadOnlyMemory<byte>>? NotificationReceived;
        public bool TryGetProgressSnapshot(out BassPlayerIpc.Shared.ProgressSnapshot snapshot) { snapshot = Progress ?? default; return Progress.HasValue; }
        public void QueueNext(BassPlayerIpc.Shared.GaplessRequest request) => Queued.Add(request);
        public async Task<long> CancelQueuedNextAsync()
        {
            Queued.Add(new(0, 0, ""));
            long token = Progress?.GaplessToken ?? 0;
            await Task.Yield();
            return token;
        }
        public void Transition(long token, long epoch)
        {
            byte[] bytes = new byte[16];
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes, token);
            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8), epoch);
            NotificationReceived?.Invoke(BassPlayerIpc.Shared.MessageTypeId.GaplessTransition, bytes);
        }
    }
    public sealed class PlaybackStatsService { public void FlushSession() { } public void StartSession(Music music) { } }
    #if !REAL_REMOTE
    public sealed class RemotePlaybackService
    {
        public int StopDelayMs;
        public int OpenDelayMs;
        public bool NeedsStart { get; set; } = true;
        public bool WantsPlay { get; private set; }
        public Task SetIntentAsync(bool? playing)
        {
            if (playing.HasValue) WantsPlay = playing.Value;
            return Task.CompletedTask;
        }
        public List<Music> Played { get; } = [];
        public event Action<Music, long, bool>? Failed;
        private long _generation;
        public int PreservedStops { get; private set; }
        public long BeginSelection(Music music, bool resumeInterrupted = false)
        {
            WantsPlay = true;
            return ++_generation;
        }
        public void Fail(Music music, bool advance = true) => Failed?.Invoke(music, _generation, advance);
        public Task StopAsync() => Task.Run(() => Thread.Sleep(StopDelayMs));
        public Task StopAsync(bool preserveFailedProgress)
        {
            if (preserveFailedProgress) { PreservedStops++; _generation++; }
            return Task.CompletedTask;
        }
        public Task PlayAsync(Music music, long generation, CancellationToken token)
        {
            Thread.Sleep(OpenDelayMs);
            token.ThrowIfCancellationRequested();
            Played.Add(music);
            return Task.CompletedTask;
        }
    }
    public sealed class WebDavLibraryService
    {
        public event Action<WebDavScanStatus>? StatusChanged;
        public event Action<int, bool>? SourceAvailabilityChanged;
        public event Action? SourcesChanged;
        public HashSet<int> Offline { get; } = [];
        public WinUIMusicPlayer.Services.WebDav.WebDavAvailability Availability { get; } = new();
        public List<int> Probes { get; } = [];
        public Func<WebDavSource, CancellationToken, Task<bool>> Probe { get; set; } = (_, _) => Task.FromResult(true);
        public bool IsSourceOffline(int source) => Offline.Contains(source);
        public WebDavScanStatus? GetStatus(int sourceId) => null;
        public void PublishAvailability(int sourceId, bool offline)
        {
            if (offline) Offline.Add(sourceId); else Offline.Remove(sourceId);
            SourceAvailabilityChanged?.Invoke(sourceId, offline);
        }
        public Task ApplyCacheSettingsAsync(WebDavCacheSettings settings) => Task.CompletedTask;
        public Task ScanAsync(WebDavSource source) => Task.CompletedTask;
        public Task CancelScanAsync(int sourceId) => Task.CompletedTask;
        public Task RemoveSourceAsync(WebDavSource source) => Task.CompletedTask;
        public async Task<(WebDavSource, object)> ResolveAsync(Music music) { await Task.Yield(); return (new(music.SourceId), new()); }
        public async Task<bool> CanPlayAsync(Music music, bool retryImmediately, CancellationToken token)
        {
            if (music.IsRemoteCached) return true;
            if (!retryImmediately && Availability.ShouldDefer(music.SourceId)) return false;
            return await EnsureAvailableAsync(new(music.SourceId), token);
        }
        public async Task<bool> EnsureAvailableAsync(WebDavSource source, CancellationToken token)
        {
            Probes.Add(source.Id);
            bool available = await Probe(source, token);
            if (!available) { Offline.Add(source.Id); Availability.ReportFailure(source.Id); }
            else Offline.Remove(source.Id);
            // Deliberately leave row flags unchanged: UI availability publication can arrive later.
            return available;
        }
    }
    public static class WebDavText { public static string Error(string code) => code; }
    #endif
}
