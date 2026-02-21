namespace DimaDB.Core.Storage.Page.Overflow;

public class OverflowPage : BasePage
{
    public OverflowPageHeader Header { get; private set; }

    public OverflowPage(uint pageId, uint nextPageId, uint dataLength)
    {
        Header = new OverflowPageHeader(pageId, nextPageId, dataLength);
        Header.Write(_buffer);
    }

    public static OverflowPage Read(byte[] buffer)
    {
        var page = new OverflowPage(0, 0, 0)
        {
            _buffer = (byte[])buffer.Clone(),
            Header = OverflowPageHeader.Read(buffer)
        };
        return page;
    }

    public void WriteData(ReadOnlySpan<byte> data)
    {
        if (data.Length > GetAvailableSpace())
        {
            throw new InvalidOperationException($"Data too large for overflow page: {data.Length} > {GetAvailableSpace()}");
        }

        data.CopyTo(_buffer.AsSpan(OverflowPageHeader.Size));
    }

    public byte[] ReadData(int length)
    {
        int maxLength = Math.Min(length, GetAvailableSpace());
        return _buffer.AsSpan(OverflowPageHeader.Size, maxLength).ToArray();
    }

    public static int GetAvailableSpace() => PageSize - OverflowPageHeader.Size;

    public void UpdateHeader(uint nextPageId)
    {
        Header = new OverflowPageHeader(Header.PageId, nextPageId, Header.DataLength);
        Header.Write(_buffer);
    }

    public override byte[] ToBytes() => _buffer;
}