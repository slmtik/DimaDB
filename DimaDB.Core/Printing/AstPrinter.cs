using DimaDB.Core.AST;
using DimaDB.Core.Parsing;
using System.Text;

namespace DimaDB.Core.Printing;

public class AstPrinter : IAstPrinter, Expression.IVisitor<string>, Statement.IVisitor<string>, Clause.IVisitor<string>, Component.IVisitor<string>
{
    public string Print(IList<Statement> statements)
    {
        var sb = new StringBuilder();

        foreach (var statement in statements)
        {
            sb.AppendLine($"\n{statement.Accept(this)}");
        }

        return sb.ToString();
    }

    private static string GetIdentifier(Identifier identifier)
    {
        var identifierSpan = identifier.Name.AsSpan();
        if (identifierSpan[0] == '"')
        {
            return identifierSpan.Slice(1, identifierSpan.Length - 2).ToString();
        }
        return identifierSpan.ToString().ToUpper();
    }

    public string VisitSelectStatement(Statement.Select statement)
    {
        var clauses = new List<string>
        {
            $"SELECT {string.Join(", ", statement.SelectItems.Select(x => x.Accept(this)))}"
        };

        if (statement.FromClause is { } fromClause)
        {
            clauses.Add($"FROM {fromClause.TableRefence.Accept(this)}");
        }

        if (statement.WhereClause is { } whereClause)
        {
            clauses.Add(whereClause.Accept(this));
        }

        if (statement.Limit is { } limit)
        {
            clauses.Add($"LIMIT {limit}");
        }

        return $"{string.Join("\n", clauses)};";
    }

    public string VisitBinaryOperationExpression(Expression.BinaryOperation expression)
    {
         return $"{expression.LeftOperand.Accept(this)} {expression.Operator.ToSymbol()} {expression.RightOperand.Accept(this)}";
    }

    public string VisitColumnReferenceExpression(Expression.ColumnReference expression)
    {
        if (expression.Table is { } table)
        {
            return $"{GetIdentifier(table)}.{GetIdentifier(expression.Column)}";
        }

        return GetIdentifier(expression.Column);
    }

    public string VisitNumberLiteralExpression(Expression.NumberLiteral expression) => expression.Value.ToString()!;

    public string VisitStringLiteralExpression(Expression.StringLiteral expression) => $"'{expression.Value.Replace("'", "''")}'";

    public string VisitTableReferenceComponent(Component.TableReference component)
    {
        if (component.Alias is { } alias)
        {
            return $"{GetIdentifier(component.Table)} AS {GetIdentifier(alias)}";
        }

        return GetIdentifier(component.Table);
    }

    public string VisitUnaryOperationExpression(Expression.UnaryOperation expression)
    {
        return $"{expression.Operator.ToSymbol()} {expression.RightOperand.Accept(this)}";
    }

    public string VisitParenthesizedExpression(Expression.Parenthesized expression) =>
        $"({expression.Expression.Accept(this)})";

    public string VisitNullLiteralExpression(Expression.NullLiteral nullLiteral) => "NULL";

    public string VisitBooleanLiteralExpression(Expression.BooleanLiteral booleanLiteral) =>
        booleanLiteral.Value ? "TRUE" : "FALSE";

    public string VisitStarComponent(Component.Star component) => "*";

    public string VisitExpressionItemComponent(Component.ExpressionItem component)
    {
        if (component.Alias is { } alias)
        {
            return $"{component.Expression.Accept(this)} AS {GetIdentifier(alias)}";
        }
        return component.Expression.Accept(this);
    }

    public string VisitQualifiedStarComponent(Component.QualifiedStar component) =>
        $"{GetIdentifier(component.Table)}.*";

    public string VisitFromClause(Clause.FromClause clause) =>
        clause.TableRefence.Accept(this);

    public string VisitCreateTableStatement(Statement.CreateTable statement)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE {GetIdentifier(statement.Table)}");
        sb.AppendLine("(");
        sb.AppendLine(string.Join(",\n", statement.ColumnDefinitions.Select(x => $"  {x.Accept(this)}")));
        sb.Append(')');

        return sb.ToString();
    }

    public string VisitColumnDefinitionComponent(Component.ColumnDefinition component)
    {
        return $"{GetIdentifier(component.Column)} {component.Type.Type}";
    }

    public string VisitInsertIntoStatement(Statement.InsertInto statement)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"INSERT INTO {GetIdentifier(statement.Table)}");
        sb.AppendLine("VALUES (");
        sb.AppendLine(string.Join(",\n", statement.Expressions.Select(x => $"  {x.Accept(this)}")));
        sb.Append(')');

        return sb.ToString();
    }

    public string VisitWhereClause(Clause.WhereClause clause)
    {
        return $"WHERE {clause.Expression.Accept(this)}";
    }
}

public static class BinaryOperatorExtensions
{
    public static string ToSymbol(this BinaryOperator op)
    {
        return op switch
        {
            BinaryOperator.Add => "+",
            BinaryOperator.Subtract => "-",
            BinaryOperator.Multiply => "*",
            BinaryOperator.Divide => "/",
            BinaryOperator.Equal => "=",
            BinaryOperator.NotEqual => "<>",
            BinaryOperator.Less => "<",
            BinaryOperator.LessEqual => "<=",
            BinaryOperator.Greater => ">",
            BinaryOperator.GreaterEqual => ">=",
            BinaryOperator.And => "AND",
            BinaryOperator.Or => "OR",
            BinaryOperator.Concat => "||",
            _ => throw new ArgumentException($"Unknown operator: {op}")
        };
    }
}

public static class UnaryOperatorExtensions
{
    public static string ToSymbol(this UnaryOperator op)
    {
        return op switch
        {
            UnaryOperator.Negate => "-",
            UnaryOperator.Not => "NOT",
            _ => throw new ArgumentException($"Unknown operator: {op}")
        };
    }
}
