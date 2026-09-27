using BassPlayerIpc.Shared;

namespace AudioPlayer.Playback;

/// <summary>Linked peak compressor, 6 dB soft knee, makeup and a -1 dBFS sample-peak ceiling.
/// Render thread owns history; immutable settings are published by the control thread.</summary>
internal sealed class DynamicsProcessor(int sampleRate, int channels)
{
    private readonly double _attack = Math.Exp(-1.0 / (sampleRate * 0.005));
    private readonly double _release = Math.Exp(-1.0 / (sampleRate * 0.150));
    private const double Ceiling = 0.8912509381337456;
    private double _reductionDb, _limiterGain = 1;
    private bool _active;

    internal void ContinueFrom(DynamicsProcessor previous)
    {
        _reductionDb = previous._reductionDb;
        _limiterGain = previous._limiterGain;
        _active = previous._active;
    }

    internal void Reset()
    {
        _reductionDb = 0;
        _limiterGain = 1;
        _active = false;
    }

    internal void Process(Span<double> samples, int frames, DspSettings settings)
    {
        if (!settings.IsEnabled || !settings.CompressorEnabled)
        {
            if (_active) Reset();
            return;
        }
        _active = true;
        double slope = 1 - 1 / settings.CompressorRatio;
        for (int frame = 0; frame < frames; frame++)
        {
            int offset = frame * channels;
            double peak = 0;
            for (int ch = 0; ch < channels; ch++)
            {
                ref double sample = ref samples[offset + ch];
                if (!double.IsFinite(sample)) sample = 0;
                peak = Math.Max(peak, Math.Abs(sample));
            }
            double over = 20 * Math.Log10(Math.Max(1e-15, peak)) - settings.CompressorThresholdDb;
            double reduction = over <= -3 ? 0 : over >= 3 ? slope * over : slope * (over + 3) * (over + 3) / 12;
            double coefficient = reduction > _reductionDb ? _attack : _release;
            _reductionDb = coefficient * _reductionDb + (1 - coefficient) * reduction;
            double gain = Math.Pow(10, (settings.CompressorMakeupDb - _reductionDb) / 20);
            // All channels receive the same gain, preserving the stereo/surround image.
            // Instant limiter attack catches transients during the compressor's attack time.
            double limit = peak > 0 ? Math.Min(1, Ceiling / peak / gain) : 1;
            _limiterGain = limit < _limiterGain ? limit : _release * _limiterGain + (1 - _release) * limit;
            gain *= _limiterGain;
            for (int ch = 0; ch < channels; ch++) samples[offset + ch] *= gain;
        }
    }
}
