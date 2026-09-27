using AudioPlayer.Decode;

namespace AudioPlayer.Playback;

/// <summary>Streaming WSOLA time stretch on the decoder thread. Channels share one alignment.
/// A 20 ms window and bounded waveform search preserve pitch; 1x bypasses this processor.
/// Storage is bounded independently of track duration and reused after seeking.</summary>
internal sealed class TempoProcessor
{
    private readonly int _channels, _hop, _search, _stride;
    private readonly double _rate;
    private readonly double[] _input, _tail, _output;
    private long _base, _produced;
    private int _count, _outputOffset, _outputFrames;
    private bool _eof, _started;

    internal TempoProcessor(int sampleRate, int channels, double rate)
    {
        _channels = channels;
        _rate = rate;
        _hop = Math.Max(16, sampleRate / 100);
        _search = Math.Max(8, sampleRate / 200);
        _stride = Math.Max(1, sampleRate / 12000);
        // Hold the next analysis hop plus its search window, including large source skips at 5x.
        _input = new double[(_hop * ((int)Math.Ceiling(rate) + 4) + _search * 2 + 16384) * channels];
        _tail = new double[_hop * channels];
        _output = new double[_hop * channels];
    }

    internal void Reset()
    {
        _base = _produced = 0;
        _count = _outputOffset = _outputFrames = 0;
        _eof = _started = false;
        Array.Clear(_tail);
    }

    internal int Read(PcmDecoder decoder, Span<double> destination)
    {
        int written = 0, requested = destination.Length / _channels;
        while (written < requested)
        {
            if (_outputOffset == _outputFrames && !Generate(decoder)) break;
            int take = Math.Min(requested - written, _outputFrames - _outputOffset);
            _output.AsSpan(_outputOffset * _channels, take * _channels)
                .CopyTo(destination[(written * _channels)..]);
            written += take;
            _outputOffset += take;
        }
        return written;
    }

    private bool Generate(PcmDecoder decoder)
    {
        long ideal = (long)Math.Round(_produced * _rate);
        long keep = Math.Max(_base, ideal - _search);
        int drop = (int)Math.Min(_count, keep - _base);
        if (drop > 0)
        {
            _input.AsSpan(drop * _channels, (_count - drop) * _channels).CopyTo(_input);
            _base += drop;
            _count -= drop;
        }
        long required = ideal + _search + 2 * _hop;
        while (!_eof && _base + _count < required)
        {
            int read = decoder.Read(_input.AsSpan(_count * _channels));
            if (read == 0) _eof = true;
            else _count += read;
        }
        long remaining = _eof ? (long)Math.Round((_base + _count) / _rate) - _produced : _hop;
        if (remaining <= 0 || _count == 0) return false;
        int frames = (int)Math.Min(_hop, remaining);
        int center = (int)Math.Clamp(ideal - _base, 0, Math.Max(0, _count - 2 * _hop));
        int chosen = center;
        if (_started && _count >= 2 * _hop)
        {
            int low = Math.Max(0, center - _search), high = Math.Min(_count - 2 * _hop, center + _search);
            double best = double.NegativeInfinity;
            // Coarse search followed by sample-accurate refinement keeps high-rate input bounded.
            for (int candidate = low; candidate <= high; candidate += _stride)
            {
                double score = Correlation(candidate);
                if (score > best) { best = score; chosen = candidate; }
            }
            int fineLow = Math.Max(low, chosen - _stride), fineHigh = Math.Min(high, chosen + _stride);
            for (int candidate = fineLow; candidate <= fineHigh; candidate++)
            {
                double score = Correlation(candidate);
                if (score > best) { best = score; chosen = candidate; }
            }
        }
        for (int frame = 0; frame < _hop; frame++)
        {
            double mix = (double)frame / _hop;
            for (int ch = 0; ch < _channels; ch++)
            {
                int i = frame * _channels + ch;
                double sample = Sample(chosen + frame, ch);
                _output[i] = _started ? _tail[i] * (1 - mix) + sample * mix : sample;
                _tail[i] = Sample(chosen + _hop + frame, ch);
            }
        }
        _started = true;
        _produced += frames;
        _outputOffset = 0;
        _outputFrames = frames;
        return true;
    }

    private double Sample(int frame, int channel) => frame < _count ? _input[frame * _channels + channel] : 0;

    private double Correlation(int start)
    {
        double dot = 0, energy = 1e-30;
        for (int frame = 0; frame < _hop; frame += _stride)
            for (int ch = 0; ch < _channels; ch++)
            {
                double sample = _input[(start + frame) * _channels + ch];
                dot += _tail[frame * _channels + ch] * sample;
                energy += sample * sample;
            }
        return dot / Math.Sqrt(energy);
    }
}
