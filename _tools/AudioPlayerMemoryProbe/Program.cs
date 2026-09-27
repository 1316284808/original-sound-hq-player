using AudioPlayer;
using AudioPlayer.Interop;
using System.Runtime;

// Diagnostic entry point only. Production sources and NativeAOT settings are shared;
// explicit collections are requested by the probe client after stopping playback.
internal static class Program
{
    private static async Task Main()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, _) => { };
        TaskScheduler.UnobservedTaskException += (_, e) => e.SetObserved();
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        using var collect = new EventWaitHandle(false, EventResetMode.AutoReset,
            "Local\\AudioPlayerMemory-" + Environment.GetEnvironmentVariable("ORIGINALSOUND_IPC_SCOPE"));
        using var stop = new CancellationTokenSource();
        var sampler = Task.Run(async () =>
        {
            long started = Environment.TickCount64;
            while (!stop.IsCancellationRequested)
            {
                if (collect.WaitOne(0))
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    Console.WriteLine("[memory] diagnostic full collection completed");
                }
                long allocated = GC.GetTotalAllocatedBytes(true);
                long heap = GC.GetTotalMemory(false);
                var info = GC.GetGCMemoryInfo();
                Console.WriteLine($"[memory] {Environment.TickCount64 - started},{allocated},{heap},{info.TotalCommittedBytes},{info.FragmentedBytes},{GC.CollectionCount(0)},{GC.CollectionCount(1)},{GC.CollectionCount(2)}");
                try { await Task.Delay(1000, stop.Token); }
                catch (OperationCanceledException) { break; }
            }
        });
        Win32.timeBeginPeriod(1);
        try { await new PlayerIpcService().StartAsync(); }
        finally
        {
            stop.Cancel();
            await sampler;
            Win32.timeEndPeriod(1);
        }
    }
}
