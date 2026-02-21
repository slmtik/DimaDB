using DimaDB.Core.Storage;
using DimaDB.Core.Storage.Types;

namespace DimaDB.Core.Tests.Storage;

public class StorageEngineTests : IDisposable
{
    private readonly string _testDir;
    private readonly StorageEngine _engine;

    public StorageEngineTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"dimadb_test_{Guid.NewGuid()}");
        _engine = new StorageEngine(_testDir);
    }

    [Fact]
    public void CreateTable_CreatesTableWithSchema()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int, false),
            new ColumnDefinition("name", ColumnType.Text, true)
        };

        _engine.CreateTable("users", columns);

        Assert.True(_engine.TableExists("users"));
        var table = _engine.OpenTable("users");
        Assert.Equal(2, table.Schema.Columns.Length);
        Assert.Equal("id", table.Schema.Columns[0].Name);
    }

    [Fact]
    public void InsertAndGet_RetrievesInsertedRecord()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("name", ColumnType.Text)
        };

        _engine.CreateTable("users", columns);
        var table = _engine.OpenTable("users");

        var values = new object?[] { 1, "Alice" };
        var rid = table.Insert(values);

        var retrieved = table.Get(rid);

        Assert.NotNull(retrieved);
        Assert.Equal(1, retrieved[0]);
        Assert.Equal("Alice", retrieved[1]);
    }

    [Fact]
    public void Update_ModifiesExistingRecord()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("name", ColumnType.Text)
        };

        _engine.CreateTable("users", columns);
        var table = _engine.OpenTable("users");

        var rid = table.Insert([1, "Alice"]);
        bool updated = table.Update(rid, [1, "Bob"]);

        Assert.True(updated);
        var retrieved = table.Get(rid);
        Assert.Equal("Bob", retrieved![1]);
    }

    [Fact]
    public void Delete_RemovesRecord()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("name", ColumnType.Text)
        };

        _engine.CreateTable("users", columns);
        var table = _engine.OpenTable("users");

        var rid = table.Insert([1, "Alice"]);
        bool deleted = table.Delete(rid);

        Assert.True(deleted);
        var retrieved = table.Get(rid);
        Assert.Null(retrieved);
    }

    [Fact]
    public void Scan_ReturnsAllInsertedRecords()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("name", ColumnType.Text)
        };

        _engine.CreateTable("users", columns);
        var table = _engine.OpenTable("users");

        table.Insert([1, "Alice"]);
        table.Insert([2, "Bob"]);
        table.Insert([3, "Charlie"]);

        var records = table.Scan().ToList();

        Assert.Equal(3, records.Count);
    }

    [Fact]
    public void Persistence_SurvivesEngineRestart()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("name", ColumnType.Text)
        };

        _engine.CreateTable("users", columns);
        var table = _engine.OpenTable("users");

        var rid = table.Insert([1, "Alice"]);
        _engine.Flush();
        _engine.Dispose();

        using var engine2 = new StorageEngine(_testDir);
        var table2 = engine2.OpenTable("users");
        var retrieved = table2.Get(rid);

        Assert.NotNull(retrieved);
        Assert.Equal("Alice", retrieved![1]);
    }

    [Fact]
    public void InsertMultiplePages_HandlesPageBoundaries()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int),
            new ColumnDefinition("data", ColumnType.Text)
        };

        _engine.CreateTable("test", columns);
        var table = _engine.OpenTable("test");

        var largeString = new string('x', 2000);

        for (int i = 0; i < 5; i++)
        {
            table.Insert([i, largeString]);
        }

        var records = table.Scan().ToList();
        Assert.Equal(5, records.Count);
    }

    [Fact]
    public void NullValues_AreHandledCorrectly()
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int, false),
            new ColumnDefinition("name", ColumnType.Text, true)
        };

        _engine.CreateTable("users", columns);
        var table = _engine.OpenTable("users");

        var rid = table.Insert([1, null]);
        var retrieved = table.Get(rid);

        Assert.NotNull(retrieved);
        Assert.Equal(1, retrieved[0]);
        Assert.Null(retrieved[1]);
    }

    [Fact]
    public void AllDataTypes_AreSerializedCorrectly()
    {
        var columns = new[]
        {
            new ColumnDefinition("int_col", ColumnType.Int),
            new ColumnDefinition("bool_col", ColumnType.Bool),
            new ColumnDefinition("text_col", ColumnType.Text)
        };

        _engine.CreateTable("alltypes", columns);
        var table = _engine.OpenTable("alltypes");

        var values = new object?[] { 42, true, "hello" };
        var rid = table.Insert(values);
        var retrieved = table.Get(rid);

        Assert.NotNull(retrieved);
        Assert.Equal(42, retrieved[0]);
        Assert.Equal(true, retrieved[1]);
        Assert.Equal("hello", retrieved[2]);
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