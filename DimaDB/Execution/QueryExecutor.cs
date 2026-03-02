using DimaDB.Planning;
using DimaDB.Storage;
using DimaDB.Parsing;
using System.Collections.Immutable;
using DimaDB.AST;
using DimaDB.Storage.Types;

namespace DimaDB.Execution;

public class QueryExecutor(StorageEngine storageEngine)
{
    public ExecutionResult Execute(QueryPlan plan)
    {
        return plan switch
        {
            QueryPlan.Select select => ExecuteSelect(select),
            QueryPlan.CreateTable createTable => ExecuteCreateTable(createTable),
            QueryPlan.InsertInto insertInto => ExecuteInsertInto(insertInto),
            QueryPlan.Delete delete => ExecuteDelete(delete),
            _ => throw new NotSupportedException($"Plan type {plan.GetType().Name} not supported")
        };
    }

    private ExecutionResult.Delete ExecuteDelete(QueryPlan.Delete delete)
    {
        var records = ExecutePlanNode(delete.Root);

        var table = storageEngine.OpenTable(delete.TableName);

        var counter = 0;
        foreach (var record in records)
        {
            table.Delete(record.RecordId);
            counter++;
        }

        return new ExecutionResult.Delete($"{counter} row(s) deleted successfully.");
    }

    private ExecutionResult.Select ExecuteSelect(QueryPlan.Select select)
    {
        var columnNames = ExtractColumnNames(select.Root);

        var records = ExecutePlanNode(select.Root);
        var rows = records.Select(x => x.Values).ToList();

        return new ExecutionResult.Select(columnNames, rows);
    }

    private ExecutionResult.CreateTable ExecuteCreateTable(QueryPlan.CreateTable createTable)
    {
        var columns = createTable.ColumnDefinitions
            .Select(cd => new ColumnDefinition(cd.Name, cd.Type, true))
            .ToArray();

        storageEngine.CreateTable(createTable.TableName, columns);

        return new ExecutionResult.CreateTable($"Table '{createTable.TableName}' created successfully.");
    }

    private ExecutionResult.InsertInto ExecuteInsertInto(QueryPlan.InsertInto insertInto)
    {
        var table = storageEngine.OpenTable(insertInto.TableName);
        var rid = table.Insert([.. insertInto.Values]);

        return new ExecutionResult.InsertInto($"Inserted record into '{insertInto.TableName}' with RID {rid}");
    }

    private IEnumerable<ResultRow> ExecutePlanNode(PlanNode node)
    {
        return node switch
        {
            PlanNode.TableScan scan => ExecuteTableScan(scan),
            PlanNode.Filter filter => ExecuteFilter(filter),
            PlanNode.Project project => ExecuteProject(project),
            PlanNode.Limit limit => ExecuteLimit(limit),
            _ => throw new NotSupportedException($"Plan node type {node.GetType().Name} not supported")
        };
    }

    private IEnumerable<ResultRow> ExecuteTableScan(PlanNode.TableScan scan)
    {
        var table = storageEngine.OpenTable(scan.TableName);
        foreach (var (rid, values) in table.Scan())
        {
            yield return new ResultRow(rid, [.. values]);
        }
    }

    private IEnumerable<ResultRow> ExecuteFilter(PlanNode.Filter filter)
    {
        var sourceRecords = ExecutePlanNode(filter.Source);
        var schema = ExtractSchema(filter.Source);

        foreach (var rowData in sourceRecords)
        {
            var record = new Row(schema, rowData.Values);
            var predicateValue = EvaluateExpression(filter.Predicate, record);

            if (predicateValue is bool boolValue && boolValue)
            {
                yield return rowData;
            }
        }
    }

    private IEnumerable<ResultRow> ExecuteProject(PlanNode.Project project)
    {
        var schema = project.Source is null ? new RowSchema([]) : ExtractSchema(project.Source);
        var sourceRecords = project.Source is null ? [new ResultRow(new RecordId(), [])] : ExecutePlanNode(project.Source);

        foreach (var rowData in sourceRecords)
        {
            var record = new Row(schema, rowData.Values);
            var projectedValues = new List<object?>();

            foreach (var item in project.Columns)
            {
                switch (item)
                {
                    case ProjectionItem.ExpandStar:
                        projectedValues.AddRange(rowData.Values);
                        break;

                    case ProjectionItem.ExpandQualifiedStar qualified:
                        for (int i = 0; i < schema.Columns.Count; i++)
                        {
                            if (schema.Columns[i].TableAlias == qualified.TableName)
                            {
                                projectedValues.Add(rowData.Values[i]);
                            }
                        }
                        break;

                    case ProjectionItem.Expression expr:
                        var value = EvaluateExpression(expr.Expr, record);
                        projectedValues.Add(value);
                        break;
                }
            }

            yield return new ResultRow(rowData.RecordId, projectedValues.AsReadOnly());
        }
    }

    private IEnumerable<ResultRow> ExecuteLimit(PlanNode.Limit limit)
    {
        var sourceRecords = ExecutePlanNode(limit.Source);
        int count = 0;

        foreach (var rowData in sourceRecords)
        {
            if (count >= limit.Count)
                break;

            yield return rowData;
            count++;
        }
    }

    private static object? EvaluateExpression(Expression expr, Row? context)
    {
        return expr switch
        {
            Expression.NumberLiteral num => num.Value,
            Expression.StringLiteral str => str.Value,
            Expression.BooleanLiteral bool_ => bool_.Value,
            Expression.NullLiteral => null,
            Expression.ColumnReference col => ResolveColumnReference(col, context!),
            Expression.BinaryOperation binOp => EvaluateBinaryOperation(binOp, context),
            Expression.UnaryOperation unOp => EvaluateUnaryOperation(unOp, context),
            _ => throw new NotSupportedException($"Expression type {expr.GetType().Name} not supported")
        };
    }

    private static object? ResolveColumnReference(Expression.ColumnReference colRef, Row context)
    {
        var columnIndex = context.Schema.FindColumnIndex(colRef.Table?.Name, colRef.Column.Name);
        if (columnIndex < 0)
            throw new InvalidOperationException($"Column '{colRef.Column.Name}' not found");

        return context.Values[columnIndex];
    }

    private static object? EvaluateBinaryOperation(Expression.BinaryOperation binOp, Row? context)
    {
        var left = EvaluateExpression(binOp.LeftOperand, context);
        var right = EvaluateExpression(binOp.RightOperand, context);

        if (left == null || right == null)
            return binOp.Operator is BinaryOperator.And or BinaryOperator.Or ? null : null;

        return binOp.Operator switch
        {
            BinaryOperator.Add => (dynamic)left + (dynamic)right,
            BinaryOperator.Subtract => (dynamic)left - (dynamic)right,
            BinaryOperator.Multiply => (dynamic)left * (dynamic)right,
            BinaryOperator.Divide => (dynamic)left / (dynamic)right,
            BinaryOperator.Equal => Equals(left, right),
            BinaryOperator.NotEqual => !Equals(left, right),
            BinaryOperator.Less => ((IComparable)left).CompareTo(right) < 0,
            BinaryOperator.LessEqual => ((IComparable)left).CompareTo(right) <= 0,
            BinaryOperator.Greater => ((IComparable)left).CompareTo(right) > 0,
            BinaryOperator.GreaterEqual => ((IComparable)left).CompareTo(right) >= 0,
            BinaryOperator.And => (bool)left && (bool)right,
            BinaryOperator.Or => (bool)left || (bool)right,
            _ => throw new NotSupportedException($"Binary operator {binOp.Operator} not supported")
        };
    }

    private static object? EvaluateUnaryOperation(Expression.UnaryOperation unOp, Row? context)
    {
        var operand = EvaluateExpression(unOp.RightOperand, context);

        return unOp.Operator switch
        {
            UnaryOperator.Negate => operand == null ? null : (object)-(dynamic)operand,
            UnaryOperator.Not => operand == null ? null : !(bool)operand,
            _ => throw new NotSupportedException($"Unary operator {unOp.Operator} not supported")
        };
    }

    private IReadOnlyList<string> ExtractColumnNames(PlanNode node)
    {
        return [.. ExtractSchema(node).Columns.Select(c => c.QualifiedName)];
    }

    private RowSchema ExtractSchema(PlanNode node)
    {
        return node switch
        {
            PlanNode.TableScan scan => ExtractTableScanSchema(scan),
            PlanNode.Filter filter => ExtractSchema(filter.Source),
            PlanNode.Project project => ExtractProjectSchema(project),
            PlanNode.Limit limit => ExtractSchema(limit.Source),
            _ => throw new NotSupportedException($"Cannot extract schema from {node.GetType().Name}")
        };
    }

    private RowSchema ExtractTableScanSchema(PlanNode.TableScan scan)
    {
        var table = storageEngine.OpenTable(scan.TableName);
        var tableAlias = scan.Alias ?? scan.TableName;

        var columns = table.Schema.Columns
            .Select(col => new ColumnSchema(col.Name, tableAlias, col.Type))
            .ToImmutableList();

        return new RowSchema(columns);
    }

    private RowSchema ExtractProjectSchema(PlanNode.Project project)
    {
        var sourceSchema = project.Source is null ? new RowSchema([]) : ExtractSchema(project.Source);

        var columns = new List<ColumnSchema>();

        foreach (var item in project.Columns)
        {
            switch (item)
            {
                case ProjectionItem.ExpandStar:
                    columns.AddRange(sourceSchema.Columns);
                    break;

                case ProjectionItem.ExpandQualifiedStar qualified:
                    var quialifiedColumns = sourceSchema.Columns.Where(c => c.TableAlias == qualified.TableName);
                    if (!quialifiedColumns.Any())
                        throw new InvalidOperationException($"No columns found for table alias '{qualified.TableName}'");
                    columns.AddRange(quialifiedColumns);
                    break;

                case ProjectionItem.Expression expr:
                    var columnName = expr.Alias ?? ExtractExpressionName(expr.Expr);
                    columns.Add(new ColumnSchema(columnName, null, null));
                    break;
            }
        }

        return new RowSchema([.. columns]);
    }

    private static string ExtractExpressionName(Expression expr)
    {
        return expr switch
        {
            Expression.ColumnReference col => col.Column.Name,
            _ => "expression"
        };
    }

    private record RowSchema(IReadOnlyList<ColumnSchema> Columns)
    {
        public int FindColumnIndex(string? tableAlias, string columnName)
        {
            for (int i = 0; i < Columns.Count; i++)
            {
                var col = Columns[i];

                if (tableAlias != null && col.TableAlias == tableAlias && col.Name == columnName)
                {
                    return i;
                }

                if (tableAlias == null && col.Name == columnName)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    private record ColumnSchema(string Name, string? TableAlias, object? Type)
    {
        public string QualifiedName => TableAlias != null ? $"{TableAlias}.{Name}" : Name;
    }

    private class Row
    {
        public RowSchema Schema { get; }
        public IReadOnlyList<object?> Values { get; }

        public Row(RowSchema schema, IReadOnlyList<object?> values)
        {
            if (schema.Columns.Count != values.Count)
                throw new InvalidOperationException("Schema columns and values count mismatch");

            Schema = schema;
            Values = values;
        }
    }

    private readonly struct ResultRow(RecordId recordId, IReadOnlyList<object?> values)
    {
        public RecordId RecordId { get; } = recordId;
        public IReadOnlyList<object?> Values { get; } = values;
    }
}