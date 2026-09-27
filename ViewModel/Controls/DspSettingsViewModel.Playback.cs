using WinUIMusicPlayer.Model;
using WinUIMusicPlayer.Services;

namespace WinUIMusicPlayer.ViewModel.Controls;

public partial class DspSettingsViewModel
{
    public bool GaplessPlayback
    {
        get => field;
        set
        {
            if (!SetProperty(ref field, value) || _syncing || !_loaded) return;
            AppSettings.Dsp = AppSettings.Dsp with { GaplessPlayback = value };
            _dirty = true;
            _commitTimer.Stop();
            _ = CommitAsync();
        }
    }

    public bool PlaybackRateEditable => EffectsActive && (_lastState?.CanChangePlaybackRate ?? false);

    public double PlaybackRate
    {
        get => field;
        set { if (double.IsFinite(value) && SetProperty(ref field, value)) SettingChanged(LicenseFeature.None); }
    }

    public bool CompressorEnabled
    {
        get => field;
        set { if (SetProperty(ref field, value)) SettingChanged(LicenseFeature.None); }
    }

    public double CompressorThresholdDb
    {
        get => field;
        set { if (double.IsFinite(value) && SetProperty(ref field, value)) SettingChanged(LicenseFeature.None); }
    }

    public double CompressorRatio
    {
        get => field;
        set { if (double.IsFinite(value) && SetProperty(ref field, value)) SettingChanged(LicenseFeature.None); }
    }

    public double CompressorMakeupDb
    {
        get => field;
        set { if (double.IsFinite(value) && SetProperty(ref field, value)) SettingChanged(LicenseFeature.None); }
    }
}
