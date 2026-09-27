using System.Buffers.Binary;

namespace BassPlayerIpc.Shared;

public readonly record struct ProgressSnapshot(long Revision, long Epoch, long CurrentMs, long TotalMs, long Timestamp, bool Playing, long SeekId = 0, double PlaybackRate = 1, long GaplessToken = 0);
public sealed record DspStateSnapshot(long Revision, DspState State);

public static class ProgressProtocol
{
    public const int Size = 72;
    public static void Write(Span<byte> bytes, ProgressSnapshot value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value.Revision);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[8..], value.Epoch);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[16..], value.CurrentMs);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[24..], value.TotalMs);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[32..], value.Timestamp);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[40..], value.Playing ? 1 : 0);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[48..], value.SeekId);
        BinaryPrimitives.WriteDoubleLittleEndian(bytes[56..], value.PlaybackRate);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[64..], value.GaplessToken);
    }

    public static ProgressSnapshot Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Size && bytes.Length != 56) throw new InvalidDataException("Invalid progress snapshot.");
        double rate = bytes.Length == Size ? BinaryPrimitives.ReadDoubleLittleEndian(bytes[56..]) : 1;
        if (!double.IsFinite(rate) || rate is < 0.25 or > 5) throw new InvalidDataException("Invalid playback rate.");
        return new(BinaryPrimitives.ReadInt64LittleEndian(bytes), BinaryPrimitives.ReadInt64LittleEndian(bytes[8..]),
            BinaryPrimitives.ReadInt64LittleEndian(bytes[16..]), BinaryPrimitives.ReadInt64LittleEndian(bytes[24..]),
            BinaryPrimitives.ReadInt64LittleEndian(bytes[32..]), BinaryPrimitives.ReadInt64LittleEndian(bytes[40..]) != 0,
            BinaryPrimitives.ReadInt64LittleEndian(bytes[48..]), rate, bytes.Length == Size ? BinaryPrimitives.ReadInt64LittleEndian(bytes[64..]) : 0);
    }
}
