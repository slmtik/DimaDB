using DimaDB.Storage;
using DimaDB.Storage.Types;

namespace DimaDB.Tests.Storage;

public class StorageEngineOverflowTests : IDisposable
{
    private readonly string _testDir;
    private readonly StorageEngine _engine;

    public StorageEngineOverflowTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"dimadb_overflow_{Guid.NewGuid()}");
        _engine = new StorageEngine(_testDir);
    }

    [Fact]
    public void SmallText_StoredInline()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("text", ColumnType.Text)
        };

        _engine.CreateTable("test", columns);
        var table = _engine.OpenTable("test");

        var smallText = "Hello, World!";
        var rid = table.Insert([1, smallText]);
        var record = table.Get(rid);

        Assert.NotNull(record);
        Assert.Equal(smallText, record![1]);
    }

    [Fact]
    public void LargeText_StoredInOverflow()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("text", ColumnType.Text)
        };

        _engine.CreateTable("test", columns);
        var table = _engine.OpenTable("test");

        var largeText = new string('x', 5000);
        var rid = table.Insert([1, largeText]);
        var record = table.Get(rid);

        Assert.NotNull(record);
        Assert.Equal(largeText, record![1]);
        Assert.Equal(5000, ((string)record![1]!).Length);
    }

    [Fact]
    public void VeryLargeText_SpansMultipleOverflowPages()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("text", ColumnType.Text)
        };

        _engine.CreateTable("test", columns);
        var table = _engine.OpenTable("test");

        var veryLargeText = new string('y', 10000);
        var rid = table.Insert([1, veryLargeText]);
        var record = table.Get(rid);

        Assert.NotNull(record);
        Assert.Equal(veryLargeText, record![1]);
    }

    [Fact]
    public void MixedInlineAndOverflow()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("short_text", ColumnType.Text),
            new ColumnDefinition("long_text", ColumnType.Text),
            new ColumnDefinition("medium_text", ColumnType.Text)
        };

        _engine.CreateTable("test", columns);
        var table = _engine.OpenTable("test");

        var shortText = "abc"; 
        var longText = new string('x', 3000); 
        var mediumText = "This is medium length text"; 

        var rid = table.Insert([1, shortText, longText, mediumText]);
        var record = table.Get(rid);

        Assert.NotNull(record);
        Assert.Equal(shortText, record![1]);
        Assert.Equal(longText, record![2]);
        Assert.Equal(mediumText, record![3]);
    }

    [Fact]
    public void OverflowPersistsAcrossRestart()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("text", ColumnType.Text)
        };

        _engine.CreateTable("test", columns);
        var table = _engine.OpenTable("test");

        var largeText = new string('z', 8000);
        var rid = table.Insert([1, largeText]);
        _engine.Flush();
        _engine.Dispose();

        var engine2 = new StorageEngine(_testDir);
        var table2 = engine2.OpenTable("test");
        var record = table2.Get(rid);

        Assert.NotNull(record);
        Assert.Equal(largeText, record![1]);

        engine2.Dispose();
    }

    [Fact]
    public void OverflowWithNull()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("text", ColumnType.Text, true)
        };

        _engine.CreateTable("test", columns);
        var table = _engine.OpenTable("test");

        var rid = table.Insert([1, null]);
        var record = table.Get(rid);

        Assert.NotNull(record);
        Assert.Null(record![1]);
    }

    [Fact]
    public void MultipleOverflowRecords()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("text", ColumnType.Text)
        };

        _engine.CreateTable("test", columns);
        var table = _engine.OpenTable("test");

        var rids = new List<RecordId>();
        for (int i = 0; i < 5; i++)
        {
            var text = new string((char)('a' + i), 3000 + i * 1000);
            var rid = table.Insert([i, text]);
            rids.Add(rid);
        }

        for (int i = 0; i < 5; i++)
        {
            var record = table.Get(rids[i]);
            Assert.NotNull(record);
            var text = new string((char)('a' + i), 3000 + i * 1000);
            Assert.Equal(text, record![1]);
        }
    }

    [Fact]
    public void UpdateOverflowText()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("text", ColumnType.Text)
        };

        _engine.CreateTable("test", columns);
        var table = _engine.OpenTable("test");

        var rid = table.Insert([1, new string('x', 5000)]);

        bool updated = table.Update(rid, [1, "Small"]);

        Assert.True(updated);
        
        var record = table.Get(rid);
        Assert.NotNull(record);
        Assert.Equal("Small", record![1]);
    }

    [Fact]
    public void ScanWithOverflowRecords()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("text", ColumnType.Text)
        };

        _engine.CreateTable("test", columns);
        var table = _engine.OpenTable("test");

        table.Insert([1, "small"]);
        table.Insert([2, new string('x', 3000)]);
        table.Insert([3, new string('y', 5000)]);

        var records = table.Scan().ToList();

        Assert.Equal(3, records.Count);
        Assert.Equal("small", records[0].Item2[1]);
        Assert.Equal(3000, ((string)records[1].Item2[1]!).Length);
        Assert.Equal(5000, ((string)records[2].Item2[1]!).Length);
    }

    public void Dispose()
    {
        _engine?.Dispose();
        GC.SuppressFinalize(this);

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