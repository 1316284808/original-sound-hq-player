using AudioPlayer.Decode;
using AudioPlayer.Playback;
using BassPlayerIpc.Shared;

internal static unsafe partial class Program
{
    private static void MeasurePlaybackAllocations()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "memory-reference.wav");
        WriteLoudnessTone(path, 0.1f);
        double[] buffer = new double[2048];
        foreach (double rate in new[] { 1.0, 0.25, 1.5, 5.0 })
        {
            using var decoder = new PcmDecoder();
            Require(decoder.Open(path, 88200, 0), "Memory decoder open failed");
            long before = GC.GetAllocatedBytesForCurrentThread();
            var tempo = rate == 1 ? null : new TempoProcessor(48000, 2, rate);
            long setupBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            // Warm wrappers/JIT before measuring the decoder-thread block loop.
            if (tempo == null) decoder.Read(buffer); else tempo.Read(decoder, buffer);
            decoder.SeekToMs(0);
            tempo?.Reset();
            before = GC.GetAllocatedBytesForCurrentThread();
            int total = 0, frames;
            while ((frames = tempo == null ? decoder.Read(buffer) : tempo.Read(decoder, buffer)) > 0) total += frames;
            long loopBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Console.WriteLine($"MEMORY rate={rate} setup={setupBytes} frames={total} loopManagedBytes={loopBytes}");
        }
        var dynamics = new DynamicsProcessor(48000, 2);
        var settings = new DspSettings { CompressorEnabled = true };
        dynamics.Process(buffer, 1024, settings);
        long initial = GC.GetAllocatedBytesForCurrentThread();
        for (int block = 0; block < 10000; block++) dynamics.Process(buffer, 1024, settings);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - initial;
        Console.WriteLine($"MEMORY dynamicsBlocks=10000 loopManagedBytes={allocated}");
        foreach (int taps in new[] { 1024, 8192 })
        {
            var impulse = new ImpulseResponse(48000, [new double[taps], new double[taps]]);
            impulse.Channels[0][0] = impulse.Channels[1][0] = 1;
            long before = GC.GetAllocatedBytesForCurrentThread();
            var coefficients = ConvolutionFilter.Prepare(impulse, 48000, 2);
            long preparationBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            before = GC.GetAllocatedBytesForCurrentThread();
            var filter = new ConvolutionFilter(coefficients);
            long historyBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(filter);
            Console.WriteLine($"MEMORY convolutionTaps={taps} coefficientPreparationBytes={preparationBytes} filterHistoryBytes={historyBytes}");
        }
    }
}
