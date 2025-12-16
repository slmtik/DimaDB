using DimaDB.SourceGenerator;

namespace DimaDB.Core.Parsing;

[AstNode("ColumnDefinition", "AST.Identifier Column, AST.TypeName Type")]
[AstNode("TableReference", "AST.Identifier Table, AST.Identifier? Alias")]
[AstNode("Star", "", "SelectItem")]
[AstNode("QualifiedStar", "AST.Identifier Table", "SelectItem")]
[AstNode("ExpressionItem", "Expression Expression, AST.Identifier? Alias", "SelectItem")]
public abstract partial record Component
{
    public abstract record SelectItem : Component;
}
