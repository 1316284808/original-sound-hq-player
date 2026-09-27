using System.Diagnostics;
using System.Runtime.CompilerServices;
using AudioPlayer;
using AudioPlayer.Playback;
using BassPlayerIpc.Shared;

internal static unsafe partial class Program
{
    private static DspState NumberedState(int number) =>
        new(0, number % 2 == 0, number, LoudnessStatus.Applied, number, -number, true);

    private static int DspWriter(string name)
    {
        using var writer = new PipeStateServer();
        using var timeout = new CancellationTokenSource(15000);
        var server = writer.RunAsync(name, Guid.NewGuid(), timeout.Token);
        for (int i = 1; i <= 10000; i++)
        {
            writer.PublishDsp(NumberedState(i));
            if (i % 100 == 0) Thread.Sleep(1);
        }
        try { server.GetAwaiter().GetResult(); }
        catch (IOException) { }
        return 0;
    }

    private static void RunDspNotificationTests()
    {
        Run("DSP pipe: late subscription, NaN deduplication and coalesced snapshots", () =>
        {
            var initial = new DspState(0, false, 0, LoudnessStatus.Off, 0, double.NaN);
            using var fixture = new StatePipeFixture(initial);
            Require(fixture.Changed.WaitOne(5000), "initial publication missing");
            Require(fixture.Read() is { Revision: 1 } first && first.State == initial, "late reader missed initial state");
            Require(!fixture.Publish(initial), "NaN causes duplicate notifications");
            try
            {
                fixture.Publish(initial with { OutputDeviceId = new string('x', 257) });
                throw new Exception("unrepresentable device identity was accepted");
            }
            catch (ArgumentException) { }
            Require(fixture.Server.CurrentDspState?.Revision == 1, "invalid publication replaced the previous state");
            for (int i = 1; i <= 100; i++) fixture.Publish(NumberedState(i));
            Require(SpinWait.SpinUntil(() => fixture.Read()?.Revision == 101, 5000), "burst lost final state");
            Require(fixture.Read()!.State == NumberedState(100), "final state is incoherent");
        });

        Run("DSP pipe: cross-process burst never exposes torn or regressing snapshots", () =>
        {
            string name = "DspTest-" + Guid.NewGuid().ToString("N");
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            info.ArgumentList.Add("--dsp-writer"); info.ArgumentList.Add(name);
            using var process = Process.Start(info)!;
            try
            {
                using var connect = new CancellationTokenSource(5000);
                using var reader = PipeStateClient.ConnectAsync(name, connect.Token).GetAwaiter().GetResult();
                using var changed = new AutoResetEvent(false);
                reader.DspStateChanged += _ => changed.Set();
                reader.Start();
                long revision = 0;
                var timeout = Stopwatch.StartNew();
                while (revision < 10000 && timeout.Elapsed < TimeSpan.FromSeconds(15))
                {
                    Require(changed.WaitOne(5000), "writer stopped notifying");
                    var snapshot = reader.CurrentDspState!;
                    Require(snapshot.Revision >= revision && snapshot.State == NumberedState((int)snapshot.Revision),
                        "snapshot torn or revision regressed");
                    revision = snapshot.Revision;
                }
                Require(revision == 10000, "final snapshot missing");
                reader.Dispose();
                Require(process.WaitForExit(5000) && process.ExitCode == 0, "writer failed");
            }
            finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(); } }
        });

        Run("DSP engine: settings, session replacement and effects changes actively publish", () =>
        {
            string name = "DspTest-" + Guid.NewGuid().ToString("N");
            using var mailbox = new StatePipeFixture();
            var service = (PlayerIpcService)RuntimeHelpers.GetUninitializedObject(typeof(PlayerIpcService));
            Set(service, "_stateServer", mailbox.Server);
            var engine = Engine("DirectSound");
            var streamLock = new object();
            Set(engine, "_streamLock", streamLock);
            Set(engine, "_ipc", service);
            using var pcm = Source(engine, RenderKind.Pcm, 44100, 2);
            using var bitstream = Source(engine, RenderKind.Dop);
            try
            {
                engine.UpdateDsp(new() { IsEnabled = false });
                Require(mailbox.Changed.WaitOne(5000) && mailbox.Read()!.State.IsEnabled == false, "idle setting change missing");
                lock (streamLock) Invoke(engine, "SetSession", pcm);
                Require(mailbox.Changed.WaitOne(5000) && mailbox.Read()!.State.Channels == 2, "PCM attachment missing");
                pcm.ConfigureDsp(new() { NormalizeLoudness = true }); // No file: Unavailable.
                Require(mailbox.Changed.WaitOne(5000) && mailbox.Read()!.State.Loudness == LoudnessStatus.Unavailable,
                    "effects state change missing");
                lock (streamLock) Invoke(engine, "SetSession", bitstream);
                Require(mailbox.Changed.WaitOne(5000) && mailbox.Read()!.State.RenderKind == (byte)RenderKind.Dop,
                    "bitstream attachment missing");
                pcm.ConfigureDsp(new());
                Require(!mailbox.Changed.WaitOne(100), "detached effects still publish");
                lock (streamLock) Invoke(engine, "SetSession", (object?)null);
                Require(mailbox.Changed.WaitOne(5000) && mailbox.Read()!.State.Channels == 0, "session removal missing");
            }
            finally
            {
                // Drain any worker before disposing the pipe publisher; avoid touching global endpoint registration.
                lock (streamLock) { Set(engine, "_disposed", 1); Invoke(engine, "SetSession", (object?)null); }
            }
        });

        Run("DSP analysis: asynchronous failure actively notifies without polling", () =>
        {
            using var effects = new PcmEffects(44100, 2);
            effects.SetFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav"), 0, 0);
            using var completed = new ManualResetEventSlim();
            effects.StateChanged += () =>
            {
                if (effects.GetState(0, false).Loudness is LoudnessStatus.Failed or LoudnessStatus.Unavailable)
                    completed.Set();
            };
            effects.Configure(new() { NormalizeLoudness = true });
            Require(completed.Wait(5000), "analysis completion did not raise state notification");
        });
    }
}
