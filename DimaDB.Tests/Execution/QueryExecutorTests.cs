using DimaDB.Execution;
using DimaDB.Planning;
using DimaDB.Parsing;
using DimaDB.Storage;
using DimaDB.Storage.Types;
using System.Collections.Immutable;
using DimaDB.AST;

namespace DimaDB.Tests.Execution;

public class QueryExecutorTests : IDisposable
{
    private readonly StorageEngine _storageEngine;
    private readonly QueryExecutor _executor;
    private readonly string _testDataDir;

    public QueryExecutorTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), $"dimadb_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDataDir);
        _storageEngine = new StorageEngine(_testDataDir);
        _executor = new QueryExecutor(_storageEngine);
    }

    public void Dispose()
    {
        _storageEngine?.Dispose();
        if (Directory.Exists(_testDataDir))
        {
            Directory.Delete(_testDataDir, true);
        }
    }

    private void CreateTestTable(string tableName = "users")
    {
        var columns = new[]
        {
            new ColumnDefinition("id", ColumnType.Int, false),
            new ColumnDefinition("name", ColumnType.Text, true),
            new ColumnDefinition("email", ColumnType.Text, true),
            new ColumnDefinition("active", ColumnType.Bool, true)
        };
        _storageEngine.CreateTable(tableName, columns);
    }

    private void InsertTestData(string tableName = "users")
    {
        var table = _storageEngine.OpenTable(tableName);
        table.Insert([1, "Alice", "alice@example.com", true]);
        table.Insert([2, "Bob", "bob@example.com", false]);
        table.Insert([3, "Charlie", "charlie@example.com", true]);
    }

    private ExecutionResult.Select ExecuteSelect(PlanNode root)
    {
        var plan = new QueryPlan.Select(root);
        var result = _executor.Execute(plan);
        return (ExecutionResult.Select)result;
    }

    [Fact]
    public void Execute_CreateTable_CreatesTableSuccessfully()
    {
        var columnDefs = new[]
        {
            new ColumnDefinition("id", ColumnType.Int, false),
            new ColumnDefinition("name", ColumnType.Text, true)
        };
        var plan = new QueryPlan.CreateTable("test_table", columnDefs);

        var result = _executor.Execute(plan);

        Assert.IsType<ExecutionResult.CreateTable>(result);
        Assert.True(_storageEngine.TableExists("test_table"));
    }

    [Fact]
    public void Execute_CreateTable_ReturnsSuccessMessage()
    {
        var columnDefs = new[] { new ColumnDefinition("id", ColumnType.Int, false) };
        var plan = new QueryPlan.CreateTable("products", columnDefs);

        var result = (ExecutionResult.CreateTable)_executor.Execute(plan);

        Assert.Contains("products", result.Message);
        Assert.Contains("created", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Execute_InsertInto_InsertsDataSuccessfully()
    {
        CreateTestTable();
        var values = new object?[] { 1, "Alice", "alice@example.com", true };
        var plan = new QueryPlan.InsertInto("users", values);

        var result = _executor.Execute(plan);

        Assert.IsType<ExecutionResult.InsertInto>(result);
        var table = _storageEngine.OpenTable("users");
        var records = table.Scan().ToList();
        Assert.Single(records);
    }

    [Fact]
    public void Execute_InsertInto_WithNullValues_HandlesNullsCorrectly()
    {
        CreateTestTable();
        var values = new object?[] { 1, null, null, false };
        var plan = new QueryPlan.InsertInto("users", values);

        _executor.Execute(plan);

        var table = _storageEngine.OpenTable("users");
        var (_, record) = table.Scan().First();
        Assert.Null(record[1]);
        Assert.Null(record[2]);
    }

    [Fact]
    public void Execute_InsertInto_ReturnsSuccessMessage()
    {
        CreateTestTable();
        var values = new object?[] { 1, "Alice", null, true };
        var plan = new QueryPlan.InsertInto("users", values);

        var result = (ExecutionResult.InsertInto)_executor.Execute(plan);

        Assert.Contains("users", result.Message);
        Assert.Contains("inserted", result.Message, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public void Execute_Select_TableScan_ReturnsAllRecords()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        var project = new PlanNode.Project(scan, [new ProjectionItem.ExpandStar()]);

        var result = ExecuteSelect(project);

        Assert.Equal(3, result.Rows.Count);
    }

    [Fact]
    public void Execute_Select_TableScan_WithAlias_UsesAliasInColumnNames()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", "u");
        var project = new PlanNode.Project(scan, [new ProjectionItem.ExpandStar()]);

        var result = ExecuteSelect(project);

        Assert.All(result.ColumnNames, name => Assert.StartsWith("u.", name));
    }

    [Fact]
    public void Execute_Select_ExpandStar_ReturnsAllColumns()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        var project = new PlanNode.Project(scan, [new ProjectionItem.ExpandStar()]);

        var result = ExecuteSelect(project);

        Assert.Equal(4, result.ColumnNames.Count);
        Assert.Contains("users.id", result.ColumnNames);
        Assert.Contains("users.name", result.ColumnNames);
        Assert.Contains("users.email", result.ColumnNames);
        Assert.Contains("users.active", result.ColumnNames);
    }

    [Fact]
    public void Execute_Select_SpecificColumns_ReturnsOnlySelectedColumns()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        var idExpr = new Expression.ColumnReference(null, new Identifier("id", false));
        var nameExpr = new Expression.ColumnReference(null, new Identifier("name", false));
        var projectionItems = ImmutableList.Create<ProjectionItem>(
            new ProjectionItem.Expression(idExpr, null),
            new ProjectionItem.Expression(nameExpr, null)
        );
        var project = new PlanNode.Project(scan, projectionItems);

        var result = ExecuteSelect(project);

        Assert.Equal(2, result.ColumnNames.Count);
        Assert.Equal(2, result.Rows[0].Count);
    }

    [Fact]
    public void Execute_Select_WithAliasedColumns_ReturnsAliasedColumnNames()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        var idExpr = new Expression.ColumnReference(null, new Identifier("id", false));
        var projectionItems = ImmutableList.Create<ProjectionItem>(
            new ProjectionItem.Expression(idExpr, "user_id")
        );
        var project = new PlanNode.Project(scan, projectionItems);

        var result = ExecuteSelect(project);

        Assert.Contains("user_id", result.ColumnNames);
    }

    [Fact]
    public void Execute_Select_QualifiedStar_ReturnsOnlyTableColumns()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        var projectionItems = ImmutableList.Create<ProjectionItem>(
            new ProjectionItem.ExpandQualifiedStar("users")
        );
        var project = new PlanNode.Project(scan, projectionItems);

        var result = ExecuteSelect(project);

        Assert.Equal(4, result.ColumnNames.Count);
        Assert.All(result.ColumnNames, name => Assert.StartsWith("users.", name));
    }

    [Fact]
    public void Execute_Select_WithEqualsFilter_ReturnsOnlyMatchingRecords()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        var left = new Expression.ColumnReference(null, new Identifier("id", false));
        var right = new Expression.NumberLiteral(1);
        var predicate = new Expression.BinaryOperation(left, BinaryOperator.Equal, right);
        var filter = new PlanNode.Filter(scan, predicate);
        var project = new PlanNode.Project(filter, [new ProjectionItem.ExpandStar()]);

        var result = ExecuteSelect(project);

        Assert.Single(result.Rows);
    }

    [Fact]
    public void Execute_Select_WithGreaterThanFilter_ReturnsMatchingRecords()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        var left = new Expression.ColumnReference(null, new Identifier("id", false));
        var right = new Expression.NumberLiteral(1);
        var predicate = new Expression.BinaryOperation(left, BinaryOperator.Greater, right);
        var filter = new PlanNode.Filter(scan, predicate);
        var project = new PlanNode.Project(filter, [new ProjectionItem.ExpandStar()]);

        var result = ExecuteSelect(project);

        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public void Execute_Select_WithAndFilter_ReturnsOnlyBothConditionsMatch()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        
        var idLeft = new Expression.ColumnReference(null, new Identifier("id", false));
        var idRight = new Expression.NumberLiteral(1);
        var idPredicate = new Expression.BinaryOperation(idLeft, BinaryOperator.Equal, idRight);
        
        var activeLeft = new Expression.ColumnReference(null, new Identifier("active", false));
        var activeRight = new Expression.BooleanLiteral(true);
        var activePredicate = new Expression.BinaryOperation(activeLeft, BinaryOperator.Equal, activeRight);
        
        var combinedPredicate = new Expression.BinaryOperation(idPredicate, BinaryOperator.And, activePredicate);
        var filter = new PlanNode.Filter(scan, combinedPredicate);
        var project = new PlanNode.Project(filter, [new ProjectionItem.ExpandStar()]);

        var result = ExecuteSelect(project);

        Assert.Single(result.Rows);
        Assert.Equal(1, result.Rows[0][0]);
    }

    [Fact]
    public void Execute_Select_WithOrFilter_ReturnsMatchingRecords()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        
        var idLeft = new Expression.ColumnReference(null, new Identifier("id", false));
        var idRight = new Expression.NumberLiteral(1);
        var idPredicate = new Expression.BinaryOperation(idLeft, BinaryOperator.Equal, idRight);
        
        var idLeft2 = new Expression.ColumnReference(null, new Identifier("id", false));
        var idRight2 = new Expression.NumberLiteral(2);
        var idPredicate2 = new Expression.BinaryOperation(idLeft2, BinaryOperator.Equal, idRight2);
        
        var combinedPredicate = new Expression.BinaryOperation(idPredicate, BinaryOperator.Or, idPredicate2);
        var filter = new PlanNode.Filter(scan, combinedPredicate);
        var project = new PlanNode.Project(filter, [new ProjectionItem.ExpandStar()]);

        var result = ExecuteSelect(project);

        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public void Execute_Select_WithLimit_ReturnsOnlyLimitedRows()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        var project = new PlanNode.Project(scan, [new ProjectionItem.ExpandStar()]);
        var limit = new PlanNode.Limit(project, 2);

        var result = ExecuteSelect(limit);

        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public void Execute_Select_WithLimitGreaterThanRecords_ReturnsAllRecords()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        var project = new PlanNode.Project(scan, [new ProjectionItem.ExpandStar()]);
        var limit = new PlanNode.Limit(project, 100);

        var result = ExecuteSelect(limit);

        Assert.Equal(3, result.Rows.Count);
    }

    [Fact]
    public void Execute_Select_WithLimitZero_ReturnsNoRows()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        var project = new PlanNode.Project(scan, [new ProjectionItem.ExpandStar()]);
        var limit = new PlanNode.Limit(project, 0);

        var result = ExecuteSelect(limit);

        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Execute_Select_WithFilterAndLimit_AppliesFilterThenLimit()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        
        var left = new Expression.ColumnReference(null, new Identifier("id", false));
        var right = new Expression.NumberLiteral(0);
        var predicate = new Expression.BinaryOperation(left, BinaryOperator.Greater, right);
        var filter = new PlanNode.Filter(scan, predicate);
        
        var project = new PlanNode.Project(filter, [new ProjectionItem.ExpandStar()]);
        var limit = new PlanNode.Limit(project, 1);

        var result = ExecuteSelect(limit);

        Assert.Single(result.Rows);
    }

    [Fact]
    public void Execute_Select_WithLiteralValues_EvaluatesCorrectly()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        
        var literal = new Expression.NumberLiteral(42);
        var projectionItems = ImmutableList.Create<ProjectionItem>(
            new ProjectionItem.Expression(literal, "answer")
        );
        var project = new PlanNode.Project(scan, projectionItems);

        var result = ExecuteSelect(project);

        Assert.Equal(3, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.Equal(42, row[0]));
    }

    [Fact]
    public void Execute_Select_WithStringLiteral_EvaluatesCorrectly()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        
        var literal = new Expression.StringLiteral("constant");
        var projectionItems = ImmutableList.Create<ProjectionItem>(
            new ProjectionItem.Expression(literal, "value")
        );
        var project = new PlanNode.Project(scan, projectionItems);

        var result = ExecuteSelect(project);

        Assert.All(result.Rows, row => Assert.Equal("constant", row[0]));
    }

    [Fact]
    public void Execute_Select_WithBooleanLiteral_EvaluatesCorrectly()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        
        var literal = new Expression.BooleanLiteral(true);
        var projectionItems = ImmutableList.Create<ProjectionItem>(
            new ProjectionItem.Expression(literal, "value")
        );
        var project = new PlanNode.Project(scan, projectionItems);

        var result = ExecuteSelect(project);

        Assert.All(result.Rows, row => Assert.True((bool)row[0]!));
    }

    [Fact]
    public void Execute_Select_WithNullLiteral_EvaluatesCorrectly()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        
        var literal = new Expression.NullLiteral();
        var projectionItems = ImmutableList.Create<ProjectionItem>(
            new ProjectionItem.Expression(literal, "value")
        );
        var project = new PlanNode.Project(scan, projectionItems);

        var result = ExecuteSelect(project);

        Assert.All(result.Rows, row => Assert.Null(row[0]));
    }

    [Fact]
    public void Execute_Select_WithAddition_EvaluatesCorrectly()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        
        var left = new Expression.NumberLiteral(10);
        var right = new Expression.NumberLiteral(5);
        var addition = new Expression.BinaryOperation(left, BinaryOperator.Add, right);
        
        var projectionItems = ImmutableList.Create<ProjectionItem>(
            new ProjectionItem.Expression(addition, "sum")
        );
        var project = new PlanNode.Project(scan, projectionItems);

        var result = ExecuteSelect(project);

        Assert.All(result.Rows, row => Assert.Equal(15, row[0]));
    }

    [Fact]
    public void Execute_Select_WithNotEqual_FilteringWorks()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        
        var left = new Expression.ColumnReference(null, new Identifier("id", false));
        var right = new Expression.NumberLiteral(1);
        var notEqual = new Expression.BinaryOperation(left, BinaryOperator.NotEqual, right);
        
        var filter = new PlanNode.Filter(scan, notEqual);
        var project = new PlanNode.Project(filter, [new ProjectionItem.ExpandStar()]);

        var result = ExecuteSelect(project);

        Assert.Equal(2, result.Rows.Count);
    }

    [Fact]
    public void Execute_Select_WithEmptyTable_ReturnsNoRows()
    {
        CreateTestTable();
        var scan = new PlanNode.TableScan("users", null);
        var project = new PlanNode.Project(scan, [new ProjectionItem.ExpandStar()]);

        var result = ExecuteSelect(project);

        Assert.Empty(result.Rows);
        Assert.NotEmpty(result.ColumnNames);
    }

    [Fact]
    public void Execute_Select_PreservesDataTypes()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        var project = new PlanNode.Project(scan, [new ProjectionItem.ExpandStar()]);

        var result = ExecuteSelect(project);

        var firstRow = result.Rows[0];
        Assert.IsType<int>(firstRow[0]);
        Assert.IsType<string>(firstRow[1]);
        Assert.IsType<string>(firstRow[2]);
        Assert.IsType<bool>(firstRow[3]);
    }

    [Fact]
    public void Execute_Delete_AllRecords()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        var delete = new QueryPlan.Delete("users", scan);

        _executor.Execute(delete);
        
        var table = _storageEngine.OpenTable("users").Scan().ToList();
        
        Assert.Empty(table);
    }

    [Fact]
    public void Execute_Delete_WithWhere()
    {
        CreateTestTable();
        InsertTestData();
        var scan = new PlanNode.TableScan("users", null);
        var filter = new PlanNode.Filter(scan, new Expression.BinaryOperation(
            new Expression.ColumnReference(null, new Identifier("id", false)),
            BinaryOperator.Equal,
            new Expression.NumberLiteral(1)
        ));
        var delete = new QueryPlan.Delete("users", filter);

        _executor.Execute(delete);

        var table = _storageEngine.OpenTable("users").Scan().ToList();

        Assert.Equal(2, table.Count);
    }
}