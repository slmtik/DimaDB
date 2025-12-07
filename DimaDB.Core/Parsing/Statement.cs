using DimaDB.SourceGenerator;

namespace DimaDB.Core.Parsing;

[AstNode("CreateTable", "Identifier Table, ImmutableArray<Expression.ColumnDefinition> ColumnDefinitions")]
[AstNode("InsertInto", "Identifier Table, ImmutableArray<Expression> Expressions")]
[AstNode("Select", "ImmutableArray<Expression.SelectItem> SelectItems, Expression.FromClause? FromClause, Expression? WhereClause, long? Limit")]
public abstract  partial record Statement
{
    public partial interface IVisitor
    {

    }

    public partial interface IVisitor<T>
    {

    }

    abstract public void Accept(IVisitor visitor);

    abstract public T Accept<T>(IVisitor<T> visitor);
}

