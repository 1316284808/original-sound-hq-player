namespace BassPlayerIpc.Shared;

/// <summary>Each connection executes requests in order. Handlers consume borrowed input synchronously.</summary>
public sealed class PipeCommandServer(string name, Guid instanceId, PipeCommandHandler handler, int instances = 1)
{
    public async Task RunAsync(CancellationToken token, bool singleSession = false)
    {
        using var workersStop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var workers = new Task[instances];
        for (int i = 0; i < workers.Length; i++)
            workers[i] = Task.Run(() => ServeAsync(workersStop.Token, singleSession), CancellationToken.None);
        try
        {
            var completed = await Task.WhenAny(workers).ConfigureAwait(false);
            await completed.ConfigureAwait(false);
        }
        finally
        {
            workersStop.Cancel();
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
    }

    private async Task ServeAsync(CancellationToken token, bool singleSession)
    {
        while (!token.IsCancellationRequested)
        {
            using var pipe = PipeProtocol.CreateServer(name, instances);
            try
            {
                await PipeProtocol.AcceptAsync(pipe, instanceId, token).ConfigureAwait(false);
                using var reader = new PipeFrameReader();
                var writer = new PipeFrameWriter();
                long lastId = 0;
                while (true)
                {
                    var frame = await reader.ReadAsync(pipe, token).ConfigureAwait(false);
                    if (frame.Kind != PipeFrameKind.Request || frame.Id <= lastId || frame.Type is < short.MinValue or > short.MaxValue)
                        throw new InvalidDataException("Invalid audio command frame.");
                    lastId = frame.Id;
                    var response = handler((CommandId)frame.Type, frame.Payload.Span);
                    await writer.WriteAsync(pipe, PipeFrameKind.Response, frame.Id, (int)response.Type, response.Payload, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException)
            {
                if (singleSession) throw;
            }
        }
    }
}
