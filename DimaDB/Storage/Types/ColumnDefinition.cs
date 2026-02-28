namespace DimaDB.Storage.Types;

public readonly record struct ColumnDefinition(string Name, ColumnType Type, bool AllowNull = true);