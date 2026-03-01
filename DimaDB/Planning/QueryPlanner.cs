using DimaDB.Parsing;
using DimaDB.Storage.Types;

namespace DimaDB.Planning;

public class QueryPlanner
{
    public QueryPlan Plan(Statement statement)
    {
        return statement switch
        {
            Statement.Select select => PlanSelect(select),
            Statement.CreateTable createTable => PlanCreateTable(createTable),
            Statement.InsertInto insertInto => PlanInsertInto(insertInto),
            _ => throw new NotSupportedException($"Unsupported statement type: {statement.GetType().Name}")
        };
    }

    private static QueryPlan.Select PlanSelect(Statement.Select select)
    {
        PlanNode plan = null!;

        if (select.FromClause != null)
        {
            plan = new PlanNode.TableScan(select.FromClause.TableRefence.Table.Name, select.FromClause.TableRefence.Alias?.Name);
        }

        if (select.WhereClause != null)
        {
            plan = new PlanNode.Filter(plan, select.WhereClause.Expression);
        }

        var projectionColumns = select.SelectItems
            .Select(ConvertSelectItemToProjection)
            .ToList()
            .AsReadOnly();

        plan = new PlanNode.Project(plan, projectionColumns);

        if (select.Limit != null)
        {
            plan = new PlanNode.Limit(plan, select.Limit.Value);
        }

        return new QueryPlan.Select(plan);
    }

    private static ProjectionItem ConvertSelectItemToProjection(Component.SelectItem item)
    {
        return item switch
        {
            Component.Star => new ProjectionItem.ExpandStar(),
            Component.QualifiedStar qualifiedStart => new ProjectionItem.ExpandQualifiedStar(qualifiedStart.Table.Name),
            Component.ExpressionItem exprItem => new ProjectionItem.Expression(exprItem.Expression, exprItem.Alias?.Name),
            _ => throw new NotSupportedException($"SelectItem type {item.GetType().Name} not supported")
        };
    }

    private static QueryPlan.CreateTable PlanCreateTable(Statement.CreateTable createTable)
    {
        var columns = createTable.ColumnDefinitions.Select(cd =>
            new ColumnDefinition(cd.Column.Name, ColumnTypeExtensions.FromString(cd.Type.Type))
        ).ToArray();

        return new QueryPlan.CreateTable(createTable.Table.Name, columns);
    }

    private static QueryPlan.InsertInto PlanInsertInto(Statement.InsertInto insertInto)
    {
        var values = insertInto.Expressions
            .Select(EvaluateExpression)
            .ToList();

        return new QueryPlan.InsertInto(insertInto.Table.Name, values);
    }

    private static object? EvaluateExpression(Expression expr)
    {
        return expr switch
        {
            Expression.NumberLiteral numLit => numLit.Value,
            Expression.StringLiteral strLit => strLit.Value,
            Expression.BooleanLiteral boolLit => boolLit.Value,
            Expression.NullLiteral => null,
            _ => throw new NotImplementedException($"Expression type {expr.GetType().Name} not supported")
        };
    }
}

