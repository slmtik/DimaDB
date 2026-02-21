namespace DimaDB.Core.Storage.Page.Data;

public readonly struct SlotDirectoryEntry(uint pageId, ushort slotId, uint offset, uint length)
{
    public const int Size = 16;
    private const uint _tombstoneMarker = 0xFFFFFFFF;
    private const uint _forwardingMarker = 0xFFFFFFFE;

    public uint PageId { get; } = pageId;
    public ushort SlotId { get; } = slotId;
    public uint Offset { get; } = offset;
    public uint Length { get; } = length;

    public bool IsTombstone => Length == _tombstoneMarker;
    public bool IsEmpty => PageId == 0 && SlotId == 0 && Offset == 0 && Length == 0;
    public bool IsForwarding => Length == _forwardingMarker;

    public static SlotDirectoryEntry Record(uint offset, uint length) => new(0, 0, offset, length);
    public static SlotDirectoryEntry Tombstone() => new(0, 0, 0, _tombstoneMarker);
    public static SlotDirectoryEntry Forwarding(uint newPageId, ushort newSlotId) 
        => new(newPageId, newSlotId, 0, _forwardingMarker);

    public static SlotDirectoryEntry Read(ReadOnlySpan<byte> buffer)
    {
        var pageId = BitConverter.ToUInt32(buffer);
        var slotId = BitConverter.ToUInt16(buffer[4..]);
        var offset = BitConverter.ToUInt32(buffer[6..]);
        var length = BitConverter.ToUInt32(buffer[10..]);
        return new(pageId, slotId, offset, length);
    }

    public void Write(Span<byte> buffer)
    {
        BitConverter.GetBytes(PageId).CopyTo(buffer);
        BitConverter.GetBytes(SlotId).CopyTo(buffer[4..]);
        BitConverter.GetBytes(Offset).CopyTo(buffer[6..]);
        BitConverter.GetBytes(Length).CopyTo(buffer[10..]);
    }
}