using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BassPlayerIpc.Shared;

public enum PlaybackSourceKind { LocalFile, Http }
public enum StreamPhase { Opening, Buffering, Ready, Playing, Paused, Ended, Failed, Stopped }
public sealed record BufferPolicy
{
    public int InitialMs { get; init; } = 1500;
    public int ResumeMs { get; init; } = 2500;
    public int CapacityMs { get; init; } = 8000;
    public int OpenTimeoutMs { get; init; } = 20000;
    public int ReadTimeoutMs { get; init; } = 15000;
    public int RetryCount { get; init; } = 2;
    public void Validate()
    {
        if (InitialMs is < 100 or > 10000 || ResumeMs is < 100 or > 10000 ||
            CapacityMs < Math.Max(InitialMs, ResumeMs) || CapacityMs > 30000 ||
            OpenTimeoutMs is < 100 or > 60000 || ReadTimeoutMs is < 100 or > 60000 || RetryCount is < 0 or > 5)
            throw new ArgumentException("Invalid buffering policy.");
    }
}
public sealed record PlaybackSource
{
    public PlaybackSourceKind Kind { get; init; }
    public string ResourceId { get; init; } = "";
    public string Location { get; init; } = "";
    /// <summary>Optional container extension (e.g. .dsf) when an opaque URL hides the filename.
    /// Used only to select a reader; the reader still validates the actual container/codec.</summary>
    public string FileExtension { get; init; } = "";
    /// <summary>Optional immutable size of the remote object, used to version background loudness analysis.</summary>
    public long ContentLength { get; init; } = -1;
    /// <summary>Optional remote entity tag, used to invalidate a cached loudness measurement after replacement.</summary>
    public string ETag { get; init; } = "";
    public Dictionary<string, string> Headers { get; init; } = [];
    public DateTimeOffset? ExpiresAt { get; init; }
    public bool CanSeek { get; init; } = true;
    public bool IsLive { get; init; }
    public BufferPolicy Buffer { get; init; } = new();
    public void Validate()
    {
        if (Buffer is null || Headers is null || Location is null || ResourceId is null || FileExtension is null || ETag is null)
            throw new ArgumentException("Invalid source.");
        Buffer.Validate();
        if (FileExtension.Length > 16 || FileExtension.Any(c => c != '.' && !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("Invalid file extension.");
        if (Location.Length is 0 or > 32768 || ResourceId.Length > 1024 || Headers.Count > 32 ||
            ContentLength < -1 || ETag.Length > 1024 || ETag.Any(c => c is '\r' or '\n' or '\0')) throw new ArgumentException("Invalid source.");
        if (ExpiresAt <= DateTimeOffset.UtcNow) throw new ArgumentException("Source expired.");
        if (Kind == PlaybackSourceKind.Http)
        {
            if (!Uri.TryCreate(Location, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))
                throw new ArgumentException("Only HTTP(S) sources are supported.");
        }
        else if (Kind != PlaybackSourceKind.LocalFile || !Path.IsPathFullyQualified(Location)) throw new ArgumentException("Invalid local source.");
        foreach (var pair in Headers)
            if (pair.Key.Length is 0 or > 128 || pair.Value is null || pair.Value.Length > 16384 ||
                pair.Key.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-') || pair.Value.Any(c => c is '\r' or '\n' or '\0'))
                throw new ArgumentException("Invalid HTTP header.");
    }
}

public sealed record StreamCommand
{
    public int Version { get; init; } = 1;
    public long RequestId { get; init; }
    public string Method { get; init; } = "capabilities";
    public Guid SessionId { get; init; }
    public PlaybackSource? Source { get; init; }
    public long PositionMs { get; init; }
    public long SeekId { get; init; }
}
public sealed record StreamReply
{
    public int Version { get; init; } = 1;
    public long RequestId { get; init; }
    public Guid SessionId { get; init; }
    public bool Accepted { get; init; }
    public string? Error { get; init; }
    public string[] Capabilities { get; init; } = [];
    public StreamPhase Phase { get; init; }
    public bool WantsPlay { get; init; }
    public bool CanSeek { get; init; }
    public long PositionMs { get; init; }
    public long? DurationMs { get; init; }
    public double PlaybackRate { get; init; } = 1;
    public long BufferedMs { get; init; }
    public long SeekId { get; init; }
}

[JsonSerializable(typeof(StreamCommand))]
[JsonSerializable(typeof(StreamReply))]
public partial class StreamingJson : JsonSerializerContext;

/// <summary>JSON streaming descriptors use the same bounded pipe framing as binary playback commands.</summary>
public static class StreamingWire
{
    public static readonly string PipeName = IpcConstants.StreamingPipeName;
    public const int MaxPayload = 128 * 1024;
}

public sealed class StreamingClient : IDisposable, IAsyncDisposable
{
    private sealed class Lane
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public PipeCommandClient? Connection;
    }
    // A slow seek/refresh or preparation cannot hold up explicit play/pause/stop or status queries.
    private readonly Lane[] _lanes = [new(), new(), new(), new()];
    private readonly CancellationTokenSource _lifetime = new();
    private long _request;
    private int _disposed;

    public async Task<StreamReply> SendAsync(StreamCommand command, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        var lane = _lanes[command.Method switch { "status" or "capabilities" => 0, "prepare" => 1, "seek" or "refresh" => 2, _ => 3 }];
        await lane.Gate.WaitAsync(timeout.Token).ConfigureAwait(false);
        byte[]? buffer = null;
        try
        {
            timeout.Token.ThrowIfCancellationRequested();
            lane.Connection ??= await PipeCommandClient.ConnectAsync(StreamingWire.PipeName, timeout.Token).ConfigureAwait(false);
            command = command with { RequestId = Interlocked.Increment(ref _request) };
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(command, StreamingJson.Default.StreamCommand);
            if (payload.Length > StreamingWire.MaxPayload) throw new InvalidDataException("Streaming descriptor too large.");
            buffer = ArrayPool<byte>.Shared.Rent(StreamingWire.MaxPayload);
            var (type, length) = await lane.Connection.RequestAsync(CommandId.StreamControl, payload, buffer,
                timeoutMs: 10000, token: timeout.Token).ConfigureAwait(false);
            if (type != MessageTypeId.Success) throw new IOException("Streaming command was not acknowledged.");
            var response = JsonSerializer.Deserialize(buffer.AsSpan(0, length), StreamingJson.Default.StreamReply)
                ?? throw new InvalidDataException("Empty streaming reply.");
            if (response.Version != 1 || response.RequestId != command.RequestId) throw new InvalidDataException("Streaming protocol mismatch.");
            return response;
        }
        catch
        {
            // Execution may already have happened. Close the failed lane, but never automatically replay a command.
            if (lane.Connection is not null) await lane.Connection.DisposeAsync().ConfigureAwait(false);
            lane.Connection = null;
            throw;
        }
        finally
        {
            if (buffer is not null) ArrayPool<byte>.Shared.Return(buffer);
            lane.Gate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        foreach (var lane in _lanes)
        {
            await lane.Gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (lane.Connection is not null) await lane.Connection.DisposeAsync().ConfigureAwait(false);
                lane.Connection = null;
            }
            finally { lane.Gate.Release(); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _lifetime.Cancel();
        await DisconnectAsync().ConfigureAwait(false);
        _lifetime.Dispose();
    }
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public Task<StreamReply> PrepareAsync(PlaybackSource source, Guid sessionId, CancellationToken ct = default)
        => SendAsync(new() { Method = "prepare", Source = source, SessionId = sessionId }, ct);
    public Task<StreamReply> PrepareAsync(PlaybackSource source, Guid sessionId, long positionMs, CancellationToken ct = default)
        => SendAsync(new() { Method = "prepare", Source = source, SessionId = sessionId, PositionMs = positionMs }, ct);
    public Task<StreamReply> PlayAsync(Guid id, CancellationToken ct = default) => SendAsync(new() { Method = "play", SessionId = id }, ct);
    public Task<StreamReply> PauseAsync(Guid id, CancellationToken ct = default) => SendAsync(new() { Method = "pause", SessionId = id }, ct);
    public Task<StreamReply> StopAsync(Guid id, CancellationToken ct = default) => SendAsync(new() { Method = "stop", SessionId = id }, ct);
    public Task<StreamReply> StatusAsync(Guid id, CancellationToken ct = default) => SendAsync(new() { Method = "status", SessionId = id }, ct);
    public Task<StreamReply> SeekAsync(Guid id, long positionMs, long seekId, CancellationToken ct = default)
        => SendAsync(new() { Method = "seek", SessionId = id, PositionMs = positionMs, SeekId = seekId }, ct);
    public Task<StreamReply> RefreshSourceAsync(Guid id, PlaybackSource source, CancellationToken ct = default)
        => SendAsync(new() { Method = "refresh", SessionId = id, Source = source }, ct);
}
