namespace DimaDB.Storage.Page.Overflow;

public readonly struct OverflowPageHeader(uint pageId, uint nextPageId, uint dataLength)
{
    public const int Size = 12;

    public uint PageId { get; } = pageId;
    public uint NextPageId { get; } = nextPageId;
    public uint DataLength { get; } = dataLength;

    public static OverflowPageHeader Read(ReadOnlySpan<byte> buffer)
    {
        var pageId = BitConverter.ToUInt32(buffer);
        var nextPageId = BitConverter.ToUInt32(buffer[4..]);
        var dataLength = BitConverter.ToUInt32(buffer[8..]);
        return new(pageId, nextPageId, dataLength);
    }

    public void Write(Span<byte> buffer)
    {
        BitConverter.GetBytes(PageId).CopyTo(buffer);
        BitConverter.GetBytes(NextPageId).CopyTo(buffer[4..]);
        BitConverter.GetBytes(DataLength).CopyTo(buffer[8..]);
    }
}