namespace DimaDB.Planning;

public abstract record PlanNode
{
    public record TableScan(string TableName, string? Alias) : PlanNode;
    public record Filter(PlanNode Source, Parsing.Expression Predicate) : PlanNode;
    public record Project(PlanNode Source, IReadOnlyList<ProjectionItem> Columns) : PlanNode;
    public record Limit(PlanNode Source, long Count ) : PlanNode;
}
