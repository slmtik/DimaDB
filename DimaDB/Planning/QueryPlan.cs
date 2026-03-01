using DimaDB.Storage.Types;

namespace DimaDB.Planning;

public abstract record QueryPlan
{
    public record Select(PlanNode Root) : QueryPlan;
    public record CreateTable(string TableName, IReadOnlyList<ColumnDefinition> ColumnDefinitions) : QueryPlan;
    public record InsertInto(string TableName, IReadOnlyList<object?> Values) : QueryPlan;
}
