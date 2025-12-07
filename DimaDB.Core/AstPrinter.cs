using DimaDB.Core.Interfaces;
using DimaDB.Core.Parsing;
using System.Text;

namespace DimaDB.Core;

public class AstPrinter : IAstPrinter, Expression.IVisitor<string>, Statement.IVisitor<string>
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

    private static string GetIdentifier(Identifier identifier) => identifier.Quoted ? $"\"{identifier.Name}\"" : identifier.Name.ToUpper();

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
            clauses.Add($"WHERE {whereClause.Accept(this)}");
        }

        if (statement.Limit is { } limit)
        {
            clauses.Add($"LIMIT {limit}");
        }

        return $"{string.Join("\n", clauses)};";
    }

    public string VisitBinaryOperationExpression(Expression.BinaryOperation expression) =>
        $"{expression.LeftOperand.Accept(this)} {expression.Operator.Lexeme} {expression.RightOperand.Accept(this)}";

    public string VisitColumnReferenceExpression(Expression.ColumnReference expression)
    {
        if (expression.Table is { } table)
        {
            return $"{GetIdentifier(table)}.{GetIdentifier(expression.Column)}";
        }

        return GetIdentifier(expression.Column);
    }

    public string VisitNumberLiteralExpression(Expression.NumberLiteral expression) => expression.Value.ToString()!;

    public string VisitStringLiteralExpression(Expression.StringLiteral expression) => $"'{expression.Value}'";

    public string VisitTableReferenceExpression(Expression.TableReference expression)
    {
        if (expression.Alias is { } alias)
        {
            return $"{GetIdentifier(expression.Table)} AS {GetIdentifier(alias)}";
        }

        return GetIdentifier(expression.Table);
    }

    public string VisitUnaryOperationExpression(Expression.UnaryOperation expression) =>
        $"{expression.Operator.Lexeme} {expression.RightOperand.Accept(this)}";

    public string VisitParenthesizedExpression(Expression.Parenthesized expression) =>
        $"({expression.Expression.Accept(this)})";

    public string VisitNullLiteralExpression(Expression.NullLiteral nullLiteral) => "NULL";

    public string VisitBooleanLiteralExpression(Expression.BooleanLiteral booleanLiteral) =>
        booleanLiteral.Value ? "TRUE" : "FALSE";

    public string VisitStarExpression(Expression.Star star) => "*";

    public string VisitSelectItemExpression(Expression.SelectItem expression)
    {
        if (expression.Alias is { } alias)
        {
            return $"{expression.Expression.Accept(this)} AS {GetIdentifier(alias)}";
        }
        return expression.Expression.Accept(this);
    }

    public string VisitQualifiedStarExpression(Expression.QualifiedStar expression) =>
        $"{GetIdentifier(expression.Table)}.*";

    public string VisitFromClauseExpression(Expression.FromClause expression) =>
        expression.TableRefence.Accept(this);

    public string VisitCreateTableStatement(Statement.CreateTable statement)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE {GetIdentifier(statement.Table)}");
        sb.AppendLine("(");
        sb.AppendLine(string.Join(",\n", statement.ColumnDefinitions.Select(x => $"  {x.Accept(this)}")));
        sb.Append(')');

        return sb.ToString();
    }

    public string VisitColumnDefinitionExpression(Expression.ColumnDefinition expression)
    {
        return $"{GetIdentifier(expression.Column)} {expression.Type.Type}";
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
}
