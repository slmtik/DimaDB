namespace DimaDB.Planning;

public abstract record ProjectionItem
{
    public sealed record ExpandStar : ProjectionItem;
    public sealed record ExpandQualifiedStar(string TableName) : ProjectionItem;
    public sealed record Expression(Parsing.Expression Expr, string? Alias) : ProjectionItem;
}
