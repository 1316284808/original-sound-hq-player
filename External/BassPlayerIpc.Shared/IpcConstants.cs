namespace BassPlayerIpc.Shared;

public static class IpcConstants
{
    // Test processes opt into private kernel objects; production uses the original names.
    public static readonly string Scope = ReadScope();
    private static string ReadScope()
    {
        string? value = Environment.GetEnvironmentVariable("ORIGINALSOUND_IPC_SCOPE");
        return !string.IsNullOrEmpty(value) && value.Length <= 64 && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') ? "_" + value : "";
    }
    public static readonly string ControlPipeName = "OriginalSound_Audio_Control_v2" + Scope;
    public static readonly string StatePipeName = "OriginalSound_Audio_State_v2" + Scope;
    public static readonly string StreamingPipeName = "OriginalSound_Audio_Streaming_v2" + Scope;
    public static readonly string MutexName = "AudioPlayer_SingleInstanceMutex" + Scope;
    public static readonly string ClientAliveMutexName = "WinUIMusicPlayer_SingleInstanceMutex" + Scope;

    public const int MaxRequestSize = 2048;
    public const int MaxResponseSize = 512;
    public const int MaxNotificationSize = 512;

    public const int MaxPayloadSize = 256 * 1024;
}
