namespace DimaDB.Execution;

public abstract record ExecutionResult
{
    public sealed record Select(IReadOnlyList<string> ColumnNames, IReadOnlyList<IReadOnlyList<object?>> Rows) : ExecutionResult;
    public sealed record CreateTable(string Message) : ExecutionResult;
    public sealed record InsertInto(string Message) : ExecutionResult;
}