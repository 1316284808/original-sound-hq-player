using AudioPlayer.Decode;

namespace AudioPlayer.Playback;

/// <summary>Streaming WSOLA time stretch on the decoder thread. Channels share one alignment.
/// A 20 ms window and bounded waveform search preserve pitch; 1x bypasses this processor.
/// Storage is bounded independently of track duration and reused after seeking.</summary>
internal sealed class TempoProcessor
{
    private readonly int _channels;
    private readonly int _hopFrames, _searchFrames, _searchStride;
    private readonly double _playbackRate;
    private readonly double[] _input, _tail, _output;
    // Source frames and output frames are different time domains when rate != 1.
    private long _inputStartFrame;
    private long _outputFramesProduced;
    private int _inputFrameCount, _outputReadOffset, _outputFrameCount;
    private bool _inputEnded, _hasOverlap;

    internal TempoProcessor(int sampleRate, int channels, double rate)
    {
        _channels = channels;
        _playbackRate = rate;
        _hopFrames = Math.Max(16, sampleRate / 100);
        _searchFrames = Math.Max(8, sampleRate / 200);
        _searchStride = Math.Max(1, sampleRate / 12000);
        // Hold the next analysis hop plus its search window, including large source skips at 5x.
        _input = new double[(_hopFrames * ((int)Math.Ceiling(rate) + 4) + _searchFrames * 2 + 16384) * channels];
        _tail = new double[_hopFrames * channels];
        _output = new double[_hopFrames * channels];
    }

    internal void Reset()
    {
        _inputStartFrame = 0;
        _outputFramesProduced = 0;
        _inputFrameCount = 0;
        _outputReadOffset = 0;
        _outputFrameCount = 0;
        _inputEnded = false;
        _hasOverlap = false;
        Array.Clear(_tail);
    }

    internal int Read(PcmDecoder decoder, Span<double> destination)
    {
        int written = 0, requested = destination.Length / _channels;
        while (written < requested)
        {
            if (_outputReadOffset == _outputFrameCount && !Generate(decoder)) break;
            int take = Math.Min(requested - written, _outputFrameCount - _outputReadOffset);
            _output.AsSpan(_outputReadOffset * _channels, take * _channels)
                .CopyTo(destination[(written * _channels)..]);
            written += take;
            _outputReadOffset += take;
        }
        return written;
    }

    private bool Generate(PcmDecoder decoder)
    {
        long ideal = (long)Math.Round(_outputFramesProduced * _playbackRate);
        long keep = Math.Max(_inputStartFrame, ideal - _searchFrames);
        int drop = (int)Math.Min(_inputFrameCount, keep - _inputStartFrame);
        if (drop > 0)
        {
            _input.AsSpan(drop * _channels, (_inputFrameCount - drop) * _channels).CopyTo(_input);
            _inputStartFrame += drop;
            _inputFrameCount -= drop;
        }
        long required = ideal + _searchFrames + 2 * _hopFrames;
        while (!_inputEnded && _inputStartFrame + _inputFrameCount < required)
        {
            int read = decoder.Read(_input.AsSpan(_inputFrameCount * _channels));
            if (read == 0) _inputEnded = true;
            else _inputFrameCount += read;
        }
        long remaining = _inputEnded ? (long)Math.Round((_inputStartFrame + _inputFrameCount) / _playbackRate) - _outputFramesProduced : _hopFrames;
        if (remaining <= 0 || _inputFrameCount == 0) return false;
        int frames = (int)Math.Min(_hopFrames, remaining);
        int center = (int)Math.Clamp(ideal - _inputStartFrame, 0, Math.Max(0, _inputFrameCount - 2 * _hopFrames));
        int chosen = center;
        if (_hasOverlap && _inputFrameCount >= 2 * _hopFrames)
        {
            int low = Math.Max(0, center - _searchFrames), high = Math.Min(_inputFrameCount - 2 * _hopFrames, center + _searchFrames);
            double best = double.NegativeInfinity;
            // Coarse search followed by sample-accurate refinement keeps high-rate input bounded.
            for (int candidate = low; candidate <= high; candidate += _searchStride)
            {
                double score = Correlation(candidate);
                if (score > best)
                {
                    best = score;
                    chosen = candidate;
                }
            }
            int fineLow = Math.Max(low, chosen - _searchStride), fineHigh = Math.Min(high, chosen + _searchStride);
            for (int candidate = fineLow; candidate <= fineHigh; candidate++)
            {
                double score = Correlation(candidate);
                if (score > best)
                {
                    best = score;
                    chosen = candidate;
                }
            }
        }
        for (int frame = 0; frame < _hopFrames; frame++)
        {
            double mix = (double)frame / _hopFrames;
            for (int ch = 0; ch < _channels; ch++)
            {
                int i = frame * _channels + ch;
                double sample = Sample(chosen + frame, ch);
                _output[i] = _hasOverlap ? _tail[i] * (1 - mix) + sample * mix : sample;
                _tail[i] = Sample(chosen + _hopFrames + frame, ch);
            }
        }
        _hasOverlap = true;
        _outputFramesProduced += frames;
        _outputReadOffset = 0;
        _outputFrameCount = frames;
        return true;
    }

    private double Sample(int frame, int channel) => frame < _inputFrameCount ? _input[frame * _channels + channel] : 0;

    private double Correlation(int start)
    {
        double dot = 0, energy = 1e-30;
        for (int frame = 0; frame < _hopFrames; frame += _searchStride)
            for (int ch = 0; ch < _channels; ch++)
            {
                double sample = _input[(start + frame) * _channels + ch];
                dot += _tail[frame * _channels + ch] * sample;
                energy += sample * sample;
            }
        return dot / Math.Sqrt(energy);
    }
}
