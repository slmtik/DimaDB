using DimaDB.SourceGenerator;

namespace DimaDB.Core.Parsing;

[AstNode("CreateTable", "AST.Identifier Table, ImmutableArray<Component.ColumnDefinition> ColumnDefinitions")]
[AstNode("InsertInto", "AST.Identifier Table, ImmutableArray<Expression> Expressions")]
[AstNode("Select", "ImmutableArray<Component.SelectItem> SelectItems, Clause.FromClause? FromClause, Clause.WhereClause? WhereClause, long? Limit")]
public abstract  partial record Statement
{
}

