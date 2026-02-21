namespace DimaDB.Core.Storage.Types;

public enum ColumnType : byte
{
    Unknown,
    Int,
    Text,
    Bool
}

public static class ColumnTypeExtensions
{
    public static ColumnType FromString(string typeName) =>
        typeName.ToUpperInvariant() switch
        {
            "INT" => ColumnType.Int,
            "TEXT" => ColumnType.Text,
            "BOOL" => ColumnType.Bool,
            _ => ColumnType.Unknown
        };

    public static string ToDisplayString(this ColumnType type) =>
        type switch
        {
            ColumnType.Int => "INT",
            ColumnType.Text => "TEXT",
            ColumnType.Bool => "BOOL",
            _ => "UNKNOWN"
        };
}