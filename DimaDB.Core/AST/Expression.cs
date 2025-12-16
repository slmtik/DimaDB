using DimaDB.SourceGenerator;

namespace DimaDB.Core.Parsing;

[AstNode("BooleanLiteral", "bool Value")]
[AstNode("BinaryOperation", "Expression LeftOperand, AST.BinaryOperator Operator, Expression RightOperand")]
[AstNode("Parenthesized", "Expression Expression")]
[AstNode("NumberLiteral", "double Value")]
[AstNode("NullLiteral", "")]
[AstNode("StringLiteral", "string Value")]
[AstNode("UnaryOperation", "AST.UnaryOperator Operator, Expression RightOperand")]
[AstNode("ColumnReference", "AST.Identifier? Table, AST.Identifier Column")]
public abstract partial record Expression
{
}