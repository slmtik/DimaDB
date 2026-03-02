using DimaDB.SourceGenerator;

namespace DimaDB.Parsing;

[AstNode("CreateTable", "AST.Identifier Table, ImmutableArray<Component.ColumnDefinition> ColumnDefinitions")]
[AstNode("InsertInto", "AST.Identifier Table, ImmutableArray<Expression> Expressions")]
[AstNode("Select", "ImmutableArray<Component.SelectItem> SelectItems, Clause.FromClause? FromClause, Clause.WhereClause? WhereClause, long? Limit")]
[AstNode("Delete", "Clause.FromClause FromClause, Clause.WhereClause? WhereClause")]
public abstract partial record Statement
{
}

