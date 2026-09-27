using BassPlayerIpc.Shared;
using CommunityToolkit.Mvvm.ComponentModel;

namespace WinUIMusicPlayer.Model
{
    public static class AppSettings { public static DspSettings Dsp { get; set; } = new(); }
}
namespace WinUIMusicPlayer.Services
{
    public enum LicenseFeature { None }
}
namespace WinUIMusicPlayer.ViewModel.Controls
{
    public partial class DspSettingsViewModel : ObservableObject
    {
        private bool _syncing = true, _loaded, _dirty;
        private DspState? _lastState;
        private readonly FakeTimer _commitTimer = new();
        public bool EffectsActive => true;
        public void LoadSavedRate()
        {
            _syncing = true;
            PlaybackRate = WinUIMusicPlayer.Model.AppSettings.Dsp.PlaybackRate;
            _loaded = true;
            _syncing = false;
        }
        private void SettingChanged(WinUIMusicPlayer.Services.LicenseFeature feature) { }
        private Task CommitAsync() => Task.CompletedTask;
        private sealed class FakeTimer { public void Stop() { } }
    }
}
