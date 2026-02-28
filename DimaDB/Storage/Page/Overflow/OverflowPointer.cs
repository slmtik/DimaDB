namespace DimaDB.Storage.Page.Overflow;

public readonly record struct OverflowPointer(uint FirstPageId, uint Length)
{
    public const int Size = 8;

    public static OverflowPointer Read(ReadOnlySpan<byte> buffer)
    {
        var firstPageId = BitConverter.ToUInt32(buffer);
        var length = BitConverter.ToUInt32(buffer[4..]);
        return new(firstPageId, length);
    }

    public void Write(Span<byte> buffer)
    {
        BitConverter.GetBytes(FirstPageId).CopyTo(buffer);
        BitConverter.GetBytes(Length).CopyTo(buffer[4..]);
    }
}