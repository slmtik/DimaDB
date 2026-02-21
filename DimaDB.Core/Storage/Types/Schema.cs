namespace DimaDB.Core.Storage.Types;

public readonly record struct Schema(string TableName, ColumnDefinition[] Columns);