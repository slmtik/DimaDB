using DimaDB.Core.Storage.IO;

namespace DimaDB.Core.Storage.Page.Overflow;

public class OverflowManager(StorageManager storageManager)
{
    private readonly StorageManager _storageManager = storageManager;
    private string _tableName = "";
    private uint _nextOverflowPageId = 1;

    public const int MaxInlineTextBytes = 250;
    public const int OverflowPageDataSize = BasePage.PageSize - OverflowPageHeader.Size;

    public void SetTableContext(string tableName)
    {
        _tableName = tableName;
    }

    public OverflowPointer StoreOverflow(byte[] textData) 
    {
        uint bytesRemaining = (uint)textData.Length;
        uint currentOffset = 0;
        uint firstPageId = AllocateOverflowPage();
        uint previousPageId = 0;

        while (bytesRemaining > 0)
        {
            uint bytesToWrite = Math.Min(bytesRemaining, OverflowPageDataSize);
            var pageData = textData.AsSpan((int)currentOffset, (int)bytesToWrite).ToArray();

            uint pageId = previousPageId == 0 ? firstPageId : AllocateOverflowPage();

            var page = new OverflowPage(pageId, 0, (uint)textData.Length);
            page.WriteData(pageData);

            if (previousPageId > 0)
            {
                var prevPage = _storageManager.GetOverflowPage(_tableName, previousPageId);
                prevPage.UpdateHeader(pageId);
                _storageManager.PutOverflowPage(_tableName, prevPage);
            }

            _storageManager.PutOverflowPage(_tableName, page);

            previousPageId = pageId;
            currentOffset += bytesToWrite;
            bytesRemaining -= bytesToWrite;
        }

        return new OverflowPointer(firstPageId, (uint)textData.Length);
    }

    public byte[] RetrieveOverflow(OverflowPointer pointer)
    {
        var result = new List<byte>();
        uint currentPageId = pointer.FirstPageId;
        uint bytesRead = 0;

        while (currentPageId > 0 && bytesRead < pointer.Length)
        {
            var page = _storageManager.GetOverflowPage(_tableName, currentPageId);
            
            uint bytesToRead = Math.Min(pointer.Length - bytesRead, OverflowPageDataSize);
            var data = page.ReadData((int)bytesToRead);
            result.AddRange(data);

            bytesRead += bytesToRead;
            currentPageId = page.Header.NextPageId;
        }

        return [..result];
    }

    public void DeleteOverflow(OverflowPointer pointer)
    {
        uint currentPageId = pointer.FirstPageId;

        while (currentPageId > 0)
        {
            var page = _storageManager.GetOverflowPage(_tableName, currentPageId);
            
            uint nextPageId = page.Header.NextPageId;

            var emptyPage = new OverflowPage(0, 0, 0);
            _storageManager.PutOverflowPage(_tableName, emptyPage);

            currentPageId = nextPageId;
        }
    }

    private uint AllocateOverflowPage()
    {
        return _nextOverflowPageId++;
    }
}