namespace DimaDB.Storage.Page.Data;

public readonly struct HeapPageHeader(uint pageId, uint numSlots, ushort freeSpaceOffset)
{
    public const int Size = 12;

    public uint PageId { get; } = pageId;
    public uint NumSlots { get; } = numSlots;
    public ushort FreeSpaceOffset { get; } = freeSpaceOffset;

    public static HeapPageHeader Read(ReadOnlySpan<byte> buffer)
    {
        var pageId = BitConverter.ToUInt32(buffer);
        var numSlots = BitConverter.ToUInt32(buffer[4..]);
        var freeOffset = BitConverter.ToUInt16(buffer[8..]);
        return new(pageId, numSlots, freeOffset);
    }

    public void Write(Span<byte> buffer)
    {
        BitConverter.GetBytes(PageId).CopyTo(buffer);
        BitConverter.GetBytes(NumSlots).CopyTo(buffer[4..]);
        BitConverter.GetBytes(FreeSpaceOffset).CopyTo(buffer[8..]);
    }
}