using DimaDB.Storage.IO;
using DimaDB.Storage.Page.Data;
using DimaDB.Storage.Page.Overflow;

namespace DimaDB.Tests.Storage;

public class StorageManagerTests : IDisposable
{
    private readonly string _testDir;
    private readonly StorageManager _storageManager;

    public StorageManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"storage_mgr_{Guid.NewGuid()}");
        _storageManager = new StorageManager(_testDir);
    }

    [Fact]
    public void GetHeapPage_CreatesNewPageIfNotExists()
    {
        var page = _storageManager.GetHeapPage("test", 0);

        Assert.NotNull(page);
        Assert.Equal(0u, page.Header.PageId);
        Assert.Equal(0u, page.Header.NumSlots);
    }

    [Fact]
    public void PutHeapPage_PersistsPage()
    {
        var page = new HeapPage(0);
        page.InsertRecord([1, 2, 3]);

        _storageManager.PutHeapPage("test", page);
        _storageManager.FlushAll();

        var retrieved = _storageManager.GetHeapPage("test", 0);
        var record = retrieved.ReadRecord(0);

        Assert.NotNull(record);
        Assert.Equal([1, 2, 3], record);
    }

    [Fact]
    public void GetOverflowPage_CreatesNewPageIfNotExists()
    {
        var page = _storageManager.GetOverflowPage("test", 0);

        Assert.NotNull(page);
        Assert.Equal(0u, page.Header.PageId);
    }

    [Fact]
    public void PutOverflowPage_PersistsPage()
    {
        var page = new OverflowPage(0, 0, 100);
        var data = new byte[] { 1, 2, 3, 4, 5 };
        page.WriteData(data);

        _storageManager.PutOverflowPage("test", page);
        _storageManager.FlushAll();

        var retrieved = _storageManager.GetOverflowPage("test", 0);
        var retrievedData = retrieved.ReadData(data.Length);

        Assert.Equal(data, retrievedData);
    }

    [Fact]
    public void MultipleHeapPages_AreIndependent()
    {
        var page0 = new HeapPage(0);
        var page1 = new HeapPage(1);

        page0.InsertRecord([1]);
        page1.InsertRecord([2]);

        _storageManager.PutHeapPage("test", page0);
        _storageManager.PutHeapPage("test", page1);

        var retrieved0 = _storageManager.GetHeapPage("test", 0);
        var retrieved1 = _storageManager.GetHeapPage("test", 1);

        Assert.Equal([1], retrieved0.ReadRecord(0));
        Assert.Equal([2], retrieved1.ReadRecord(0));
    }

    [Fact]
    public void SeparateTables_HaveSeparateFiles()
    {
        var page1 = new HeapPage(0);
        page1.InsertRecord([1]);

        var page2 = new HeapPage(0);
        page2.InsertRecord([2]);

        _storageManager.PutHeapPage("table1", page1);
        _storageManager.PutHeapPage("table2", page2);
        _storageManager.FlushAll();

        var file1 = File.Exists(Path.Combine(_testDir, "table1.db"));
        var file2 = File.Exists(Path.Combine(_testDir, "table2.db"));

        Assert.True(file1);
        Assert.True(file2);
    }

    [Fact]
    public void OverflowPages_StoredSeparately()
    {
        var heapPage = new HeapPage(0);
        heapPage.InsertRecord([1]);

        var overflowPage = new OverflowPage(0, 0, 100);
        overflowPage.WriteData([2]);

        _storageManager.PutHeapPage("test", heapPage);
        _storageManager.PutOverflowPage("test", overflowPage);
        _storageManager.FlushAll();

        var heapFile = File.Exists(Path.Combine(_testDir, "test.db"));
        var overflowFile = File.Exists(Path.Combine(_testDir, "test.overflow.db"));

        Assert.True(heapFile);
        Assert.True(overflowFile);
    }

    public void Dispose()
    {
        _storageManager?.Dispose();

        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch { }
        }
    }
}