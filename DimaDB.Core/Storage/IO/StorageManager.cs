using DimaDB.Core.Storage.Page;
using DimaDB.Core.Storage.Page.Data;
using DimaDB.Core.Storage.Page.Overflow;

namespace DimaDB.Core.Storage.IO;

public class StorageManager : IDisposable
{
    private readonly string _dataDir;
    private readonly BufferPool _bufferPool;
    private readonly Dictionary<string, FileStream> _tableFiles = [];
    private readonly Dictionary<string, FileStream> _overflowFiles = [];
    private readonly Dictionary<string, uint> _maxPageIds = [];

    public StorageManager(string dataDir)
    {
        _dataDir = dataDir;
        _bufferPool = new BufferPool(100);

        if (!Directory.Exists(_dataDir))
        {
            Directory.CreateDirectory(_dataDir);
        }
    }

    public HeapPage GetHeapPage(string tableName, uint pageId)
    {
        string cacheKey = MakeCacheKey("heap", tableName, pageId);
        var cached = _bufferPool.GetOrNull(cacheKey);
        if (cached is HeapPage heapPage)
        {
            return heapPage;
        }

        var file = GetOrOpenTableFile(tableName);
        long offset = pageId * BasePage.PageSize;

        if (offset + BasePage.PageSize > file.Length)
        {
            return new HeapPage(pageId);
        }

        var buffer = new byte[BasePage.PageSize];
        file.Seek(offset, SeekOrigin.Begin);
        file.Read(buffer);

        var page = HeapPage.Read(buffer);
        _bufferPool.Put(cacheKey, page);
        return page;
    }

    public OverflowPage GetOverflowPage(string tableName, uint pageId)
    {
        string cacheKey = MakeCacheKey("overflow", tableName, pageId);
        var cached = _bufferPool.GetOrNull(cacheKey);
        if (cached is OverflowPage overflowPage)
        {
            return overflowPage;
        }

        var file = GetOrOpenOverflowFile(tableName);
        long offset = pageId * BasePage.PageSize;

        if (offset + BasePage.PageSize > file.Length)
        {
            return new OverflowPage(pageId, 0, 0);
        }

        var buffer = new byte[BasePage.PageSize];
        file.Seek(offset, SeekOrigin.Begin);
        file.Read(buffer);

        var page = OverflowPage.Read(buffer);
        _bufferPool.Put(cacheKey, page);
        return page;
    }

    public void PutHeapPage(string tableName, HeapPage page)
    {
        string cacheKey = MakeCacheKey("heap", tableName, page.Header.PageId);
        _bufferPool.Put(cacheKey, page);

        if (!_maxPageIds.TryGetValue(tableName, out uint value) || value < page.Header.PageId)
        {
            value = page.Header.PageId;
            _maxPageIds[tableName] = value;
        }

        var file = GetOrOpenTableFile(tableName);
        long offset = page.Header.PageId * BasePage.PageSize;

        file.Seek(offset, SeekOrigin.Begin);
        file.Write(page.ToBytes());
        file.Flush();
    }

    public void PutOverflowPage(string tableName, OverflowPage page)
    {
        string cacheKey = MakeCacheKey("overflow", tableName, page.Header.PageId);
        _bufferPool.Put(cacheKey, page);

        var file = GetOrOpenOverflowFile(tableName);
        long offset = page.Header.PageId * BasePage.PageSize;

        file.Seek(offset, SeekOrigin.Begin);
        file.Write(page.ToBytes());
        file.Flush();
    }

    public uint GetMaxPageId(string tableName)
    {
        if (_maxPageIds.TryGetValue(tableName, out var maxId))
        {
            return maxId;
        }

        var file = GetOrOpenTableFile(tableName);
        uint estimatedPages = (uint)(file.Length / BasePage.PageSize);

        if (estimatedPages > 0)
        {
            _maxPageIds[tableName] = estimatedPages - 1;
        }

        return estimatedPages > 0 ? estimatedPages - 1 : 0;
    }

    public void FlushAll()
    {
        foreach (var file in _tableFiles.Values)
        {
            file.Flush();
        }

        foreach (var file in _overflowFiles.Values)
        {
            file.Flush();
        }
    }

    private FileStream GetOrOpenTableFile(string tableName)
    {
        if (_tableFiles.TryGetValue(tableName, out var file))
        {
            return file;
        }

        string filePath = Path.Combine(_dataDir, $"{tableName}.db");
        var stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, BasePage.PageSize);
        _tableFiles[tableName] = stream;
        return stream;
    }

    private FileStream GetOrOpenOverflowFile(string tableName)
    {
        if (_overflowFiles.TryGetValue(tableName, out var file))
        {
            return file;
        }

        string filePath = Path.Combine(_dataDir, $"{tableName}.overflow.db");
        var stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, BasePage.PageSize);
        _overflowFiles[tableName] = stream;
        return stream;
    }

    private static string MakeCacheKey(string pageType, string tableName, uint pageId)
    {
        return $"{pageType}:{tableName}:{pageId}";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var file in _tableFiles.Values)
            {
                file?.Dispose();
            }
            _tableFiles.Clear();

            foreach (var file in _overflowFiles.Values)
            {
                file?.Dispose();
            }
            _overflowFiles.Clear();
        }
    }
}