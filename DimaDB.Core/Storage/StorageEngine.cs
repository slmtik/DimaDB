using DimaDB.Core.Storage.IO;
using DimaDB.Core.Storage.Page.Overflow;
using DimaDB.Core.Storage.Types;

namespace DimaDB.Core.Storage;

public class StorageEngine : IDisposable
{
    private readonly StorageManager _storageManager;
    private readonly SchemaStore _schemaStore;
    private readonly OverflowManager _overflowManager;
    private readonly FreeSpaceMap _freeSpaceMap;
    private readonly Dictionary<string, TableHandle> _openTables = [];

    public StorageEngine(string dataDir = "./data")
    {
        _storageManager = new StorageManager(dataDir);
        _schemaStore = new SchemaStore(dataDir);
        _overflowManager = new OverflowManager(_storageManager);
        _freeSpaceMap = new FreeSpaceMap();
    }

    public void CreateTable(string tableName, ColumnDefinition[] columns)
    {
        if (_schemaStore.TableExists(tableName))
            throw new InvalidOperationException($"Table {tableName} already exists");

        var schema = new Schema(tableName, columns);
        _schemaStore.SaveSchema(schema);
    }

    public void DropTable(string tableName)
    {
        if (_openTables.TryGetValue(tableName, out var _))
        {
            _openTables.Remove(tableName);
        }

        _schemaStore.DeleteTable(tableName);

        string filePath = Path.Combine("./data", $"{tableName}.db");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        _freeSpaceMap.Clear();
    }

    public TableHandle OpenTable(string tableName)
    {
        if (_openTables.TryGetValue(tableName, out var cached))
            return cached;

        var schema = _schemaStore.LoadSchema(tableName) ?? throw new InvalidOperationException($"Table {tableName} not found");

        var handle = new TableHandle(tableName, schema, _storageManager, _overflowManager, _freeSpaceMap);
        _openTables[tableName] = handle;
        return handle;
    }

    public IEnumerable<string> GetTableNames() => _schemaStore.GetTableNames();

    public bool TableExists(string tableName) => _schemaStore.TableExists(tableName);

    public void Flush() => _storageManager.FlushAll();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _storageManager?.Dispose();
        }
    }
}