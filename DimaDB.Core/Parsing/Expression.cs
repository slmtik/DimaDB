using DimaDB.SourceGenerator;

namespace DimaDB.Core.Parsing;

[AstNode("BooleanLiteral", "bool Value")]
[AstNode("BinaryOperation", "Expression LeftOperand, Token Operator, Expression RightOperand")]
[AstNode("Parenthesized", "Expression Expression")]
[AstNode("NumberLiteral", "double Value")]
[AstNode("NullLiteral", "")]
[AstNode("StringLiteral", "string Value")]
[AstNode("UnaryOperation", "Token Operator, Expression RightOperand")]
[AstNode("SelectItem", "Expression Expression, Identifier? Alias")]
[AstNode("Star", "")]
[AstNode("QualifiedStar", "Identifier Table")]
[AstNode("ColumnReference", "Identifier? Table, Identifier Column")]
[AstNode("FromClause", "TableReference TableRefence")]
[AstNode("TableReference", "Identifier Table, Identifier? Alias")]
[AstNode("ColumnDefinition", "Identifier Column, TypeName Type")]
public abstract partial record Expression
{
    public partial interface IVisitor<T>
    {

    }

    abstract public T Accept<T>(IVisitor<T> visitor);
}