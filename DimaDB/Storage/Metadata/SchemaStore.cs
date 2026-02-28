using DimaDB.Storage.Types;
using System.Text.Json;

namespace DimaDB.Storage;

public class SchemaStore
{
    private readonly string _metadataPath;
    private readonly Dictionary<string, SchemaMetadata> _schemas = [];

    private record SchemaMetadata(string TableName, ColumnMetadata[] Columns);
    private record ColumnMetadata(string Name, string Type, bool AllowNull);

    public SchemaStore(string dataDir)
    {
        _metadataPath = Path.Combine(dataDir, "schemas.json");
        LoadSchemas();
    }

    public void SaveSchema(Schema schema)
    {
        var colMeta = schema.Columns.Select(c =>
            new ColumnMetadata(c.Name, c.Type.ToDisplayString(), c.AllowNull)
        ).ToArray();

        _schemas[schema.TableName] = new SchemaMetadata(schema.TableName, colMeta);
        PersistSchemas();
    }

    public Schema? LoadSchema(string tableName)
    {
        if (!_schemas.TryGetValue(tableName, out var meta))
            return null;

        var columns = meta.Columns.Select(c =>
            new ColumnDefinition(c.Name, ColumnTypeExtensions.FromString(c.Type), c.AllowNull)
        ).ToArray();

        return new Schema(tableName, columns);
    }

    public IEnumerable<string> GetTableNames() => _schemas.Keys;

    public bool TableExists(string tableName) => _schemas.ContainsKey(tableName);

    public void DeleteTable(string tableName)
    {
        _schemas.Remove(tableName);
        PersistSchemas();
    }

    private void LoadSchemas()
    {
        _schemas.Clear();

        if (!File.Exists(_metadataPath))
            return;

        try
        {
            var json = File.ReadAllText(_metadataPath);
            var list = JsonSerializer.Deserialize<List<SchemaMetadata>>(json) ?? [];

            foreach (var meta in list)
            {
                _schemas[meta.TableName] = meta;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: failed to load schemas: {ex.Message}");
        }
    }

    private void PersistSchemas()
    {
        try
        {
            var list = _schemas.Values.ToList();
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_metadataPath, json);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: failed to save schemas: {ex.Message}");
        }
    }
}