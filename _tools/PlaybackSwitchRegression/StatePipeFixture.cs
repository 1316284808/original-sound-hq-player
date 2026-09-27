using BassPlayerIpc.Shared;

/// <summary>Engine tests use the production server, framing, asynchronous pipe reader and cached state.</summary>
internal sealed class StatePipeFixture : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serverTask;
    public PipeStateServer Server { get; } = new();
    public PipeStateClient Client { get; }
    public AutoResetEvent Changed { get; } = new(false);

    public StatePipeFixture(DspState? initial = null)
    {
        string name = "StateTest-" + Guid.NewGuid().ToString("N");
        if (initial is not null) Server.PublishDsp(initial.Value);
        _serverTask = Task.Run(() => Server.RunAsync(name, Guid.NewGuid(), _stop.Token));
        using var timeout = new CancellationTokenSource(5000);
        Client = PipeStateClient.ConnectAsync(name, timeout.Token).GetAwaiter().GetResult();
        Client.DspStateChanged += _ => Changed.Set();
        Client.Start();
    }

    public bool Publish(DspState state) => Server.PublishDsp(state);
    public DspStateSnapshot? Read() => Client.CurrentDspState;
    public void Dispose()
    {
        _stop.Cancel();
        Client.Dispose();
        try { _serverTask.GetAwaiter().GetResult(); }
        catch (Exception ex) when (ex is IOException or OperationCanceledException) { }
        Server.Dispose();
        Changed.Dispose();
        _stop.Dispose();
    }
}
