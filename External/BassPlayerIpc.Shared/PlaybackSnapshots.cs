using System.Buffers.Binary;

namespace BassPlayerIpc.Shared;

public readonly record struct ProgressSnapshot(long Revision, long Epoch, long CurrentMs, long TotalMs, long Timestamp, bool Playing, long SeekId = 0);
public sealed record DspStateSnapshot(long Revision, DspState State);

public static class ProgressProtocol
{
    public const int Size = 56;
    public static void Write(Span<byte> bytes, ProgressSnapshot value)
    {
        BinaryPrimitives.WriteInt64LittleEndian(bytes, value.Revision);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[8..], value.Epoch);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[16..], value.CurrentMs);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[24..], value.TotalMs);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[32..], value.Timestamp);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[40..], value.Playing ? 1 : 0);
        BinaryPrimitives.WriteInt64LittleEndian(bytes[48..], value.SeekId);
    }

    public static ProgressSnapshot Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Size) throw new InvalidDataException("Invalid progress snapshot.");
        return new(BinaryPrimitives.ReadInt64LittleEndian(bytes), BinaryPrimitives.ReadInt64LittleEndian(bytes[8..]),
            BinaryPrimitives.ReadInt64LittleEndian(bytes[16..]), BinaryPrimitives.ReadInt64LittleEndian(bytes[24..]),
            BinaryPrimitives.ReadInt64LittleEndian(bytes[32..]), BinaryPrimitives.ReadInt64LittleEndian(bytes[40..]) != 0,
            BinaryPrimitives.ReadInt64LittleEndian(bytes[48..]));
    }
}
