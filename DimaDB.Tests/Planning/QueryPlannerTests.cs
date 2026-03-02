using DimaDB.Planning;
using DimaDB.Parsing;
using DimaDB.Lexing;
using DimaDB.AST;

namespace DimaDB.Tests.Planning;

public class QueryPlannerTests
{
    private readonly QueryPlanner _planner = new();
    private readonly Lexer _lexer = new();
    private readonly Parser _parser = new();

    private QueryPlan Plan(string sql)
    {
        var tokens = _lexer.Tokenize(sql);
        var statements = _parser.Parse(sql, tokens);
        return _planner.Plan(statements[0]);
    }

    [Fact]
    public void Plan_CreateTable_WithSingleColumn_ReturnsCreateTablePlan()
    {
        const string sql = "CREATE TABLE users (id INT);";

        var plan = Plan(sql);

        Assert.IsType<QueryPlan.CreateTable>(plan);
        var createTable = (QueryPlan.CreateTable)plan;
        Assert.Equal("users", createTable.TableName);
        Assert.Single(createTable.ColumnDefinitions);
        Assert.Equal("id", createTable.ColumnDefinitions[0].Name);
    }

    [Fact]
    public void Plan_CreateTable_WithMultipleColumns_ReturnsAllColumns()
    {
        const string sql = "CREATE TABLE users (id INT, name TEXT, active BOOL);";

        var plan = Plan(sql);

        var createTable = (QueryPlan.CreateTable)plan;
        Assert.Equal(3, createTable.ColumnDefinitions.Count);
        Assert.Equal("id", createTable.ColumnDefinitions[0].Name);
        Assert.Equal("name", createTable.ColumnDefinitions[1].Name);
        Assert.Equal("active", createTable.ColumnDefinitions[2].Name);
    }

    [Fact]
    public void Plan_CreateTable_PreservesColumnTypes()
    {
        const string sql = "CREATE TABLE data (id INT, description TEXT);";

        var plan = Plan(sql);

        var createTable = (QueryPlan.CreateTable)plan;
        Assert.Equal(DimaDB.Storage.Types.ColumnType.Int, createTable.ColumnDefinitions[0].Type);
        Assert.Equal(DimaDB.Storage.Types.ColumnType.Text, createTable.ColumnDefinitions[1].Type);
    }

    [Fact]
    public void Plan_InsertInto_WithIntegerValues_ReturnsInsertPlan()
    {
        const string sql = "INSERT INTO users VALUES (1);";

        var plan = Plan(sql);

        Assert.IsType<QueryPlan.InsertInto>(plan);
        var insert = (QueryPlan.InsertInto)plan;
        Assert.Equal("users", insert.TableName);
        Assert.Single(insert.Values);
        Assert.Equal(1, insert.Values[0]);
    }

    [Fact]
    public void Plan_InsertInto_WithMultipleValues_ReturnsAllValues()
    {
        const string sql = "INSERT INTO users VALUES (42, 'Alice', TRUE);";

        var plan = Plan(sql);

        var insert = (QueryPlan.InsertInto)plan;
        Assert.Equal(3, insert.Values.Count);
        Assert.Equal(42, insert.Values[0]);
        Assert.Equal("Alice", insert.Values[1]);
        Assert.Equal(true, insert.Values[2]);
    }

    [Fact]
    public void Plan_InsertInto_WithNullValue_PreservesNull()
    {
        const string sql = "INSERT INTO users VALUES (1, NULL);";

        var plan = Plan(sql);

        var insert = (QueryPlan.InsertInto)plan;
        Assert.Equal(2, insert.Values.Count);
        Assert.Null(insert.Values[1]);
    }

    [Fact]
    public void Plan_InsertInto_WithStringLiteral_ParsesString()
    {
        const string sql = "INSERT INTO users VALUES ('test@example.com');";

        var plan = Plan(sql);

        var insert = (QueryPlan.InsertInto)plan;
        Assert.Equal("test@example.com", insert.Values[0]);
    }


    [Fact]
    public void Plan_Select_WithTableScan_BuildsTableScanNode()
    {
        const string sql = "SELECT * FROM users;";

        var plan = Plan(sql);

        Assert.IsType<QueryPlan.Select>(plan);
        var select = (QueryPlan.Select)plan;

        Assert.IsType<PlanNode.Project>(select.Root);
    }

    [Fact]
    public void Plan_Select_WithWhereClause_BuildsFilterNode()
    {
        const string sql = "SELECT * FROM users WHERE id = 1;";

        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        var project = (PlanNode.Project)select.Root;

        Assert.IsType<PlanNode.Filter>(project.Source);
    }

    [Fact]
    public void Plan_Select_WithLimit_BuildsLimitNode()
    {
        const string sql = "SELECT * FROM users LIMIT 10;";

        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        var limit = select.Root as PlanNode.Limit;

        Assert.NotNull(limit);
        Assert.Equal(10, limit!.Count);
    }

    [Fact]
    public void Plan_Select_WithWhereAndLimit_BuildsCorrectPipeline()
    {
        const string sql = "SELECT * FROM users WHERE id > 5 LIMIT 20;";

        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        var limit = (PlanNode.Limit)select.Root;
        var project = (PlanNode.Project)limit.Source;
        var filter = (PlanNode.Filter)project.Source;
        var scan = (PlanNode.TableScan)filter.Source;

        Assert.Equal(20, limit.Count);
        Assert.Equal("users", scan.TableName);
    }

    [Fact]
    public void Plan_Select_WithStar_CreatesExpandStarProjection()
    {
        const string sql = "SELECT * FROM users;";

        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        var project = (PlanNode.Project)select.Root;

        Assert.Single(project.Columns);
        Assert.IsType<ProjectionItem.ExpandStar>(project.Columns[0]);
    }

    [Fact]
    public void Plan_Select_WithQualifiedStar_CreatesExpandQualifiedStarProjection()
    {
        const string sql = "SELECT users.* FROM users;";

        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        var project = (PlanNode.Project)select.Root;

        var qualified = project.Columns[0] as ProjectionItem.ExpandQualifiedStar;
        Assert.NotNull(qualified);
        Assert.Equal("users", qualified!.TableName);
    }

    [Fact]
    public void Plan_Select_WithColumnExpression_CreatesExpressionProjection()
    {
        const string sql = "SELECT id FROM users;";

        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        var project = (PlanNode.Project)select.Root;

        var expr = project.Columns[0] as ProjectionItem.Expression;
        Assert.NotNull(expr);
        Assert.Null(expr!.Alias);
    }

    [Fact]
    public void Plan_Select_WithAliasedColumn_PreservesAlias()
    {
        const string sql = "SELECT id AS user_id FROM users;";

        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        var project = (PlanNode.Project)select.Root;

        var expr = (ProjectionItem.Expression)project.Columns[0];
        Assert.Equal("user_id", expr.Alias);
    }

    [Fact]
    public void Plan_Select_WithMultipleColumns_CreatesMultipleProjections()
    {
        const string sql = "SELECT id, name, email FROM users;";

        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        var project = (PlanNode.Project)select.Root;

        Assert.Equal(3, project.Columns.Count);
    }

    [Fact]
    public void Plan_Select_WithTableAlias_PreservesAliasInTableScan()
    {
        const string sql = "SELECT * FROM users AS u;";

        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        var project = (PlanNode.Project)select.Root;
        var scan = (PlanNode.TableScan)project.Source;

        Assert.Equal("users", scan.TableName);
        Assert.Equal("u", scan.Alias);
    }

    [Fact]
    public void Plan_Select_WithoutAlias_SetsAliasToNull()
    {
        const string sql = "SELECT * FROM products;";

        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        var project = (PlanNode.Project)select.Root;
        var scan = (PlanNode.TableScan)project.Source;

        Assert.Null(scan.Alias);
    }

    [Fact]
    public void Plan_Select_WithSimpleWhereClause_ExtractsPredicateExpression()
    {
        const string sql = "SELECT * FROM users WHERE id = 1;";

        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        var project = (PlanNode.Project)select.Root;
        var filter = (PlanNode.Filter)project.Source;

        Assert.NotNull(filter.Predicate);
        Assert.IsType<Expression.BinaryOperation>(filter.Predicate);
    }

    [Fact]
    public void Plan_Select_WithComplexWhereClause_PreservesBinaryOperationStructure()
    {
        const string sql = "SELECT * FROM users WHERE id > 5 AND active = TRUE;";

        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        var project = (PlanNode.Project)select.Root;
        var filter = (PlanNode.Filter)project.Source;
        var binOp = (Expression.BinaryOperation)filter.Predicate;

        Assert.Equal(BinaryOperator.And, binOp.Operator);
    }

    [Fact]
    public void Plan_InsertInto_WithBooleanValue_PreservesBoolType()
    {
        const string sql = "INSERT INTO users VALUES (FALSE);";

        var plan = Plan(sql);

        var insert = (QueryPlan.InsertInto)plan;
        Assert.Equal(false, insert.Values[0]);
    }

    [Fact]
    public void Plan_Select_WithIntegerNumber_PreservesIntegerValue()
    {
        const string sql = "SELECT * FROM products WHERE price = 19;";

        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        var project = (PlanNode.Project)select.Root;
        var filter = (PlanNode.Filter)project.Source;
        var binOp = (Expression.BinaryOperation)filter.Predicate;
        var literal = (Expression.NumberLiteral)binOp.RightOperand;

        Assert.Equal(19, literal.Value);
    }

    [Theory]
    [InlineData("SELECT * FROM t1;")]
    [InlineData("SELECT id FROM t1;")]
    [InlineData("SELECT id, name FROM t1;")]
    public void Plan_Select_AlwaysEndsWithProjection(string sql)
    {
        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        Assert.IsType<PlanNode.Project>(select.Root);
    }

    [Theory]
    [InlineData("SELECT * FROM t1 WHERE x = 1;")]
    [InlineData("SELECT id FROM t1 WHERE id > 5;")]
    public void Plan_Select_WithWhere_PlacesFilterBeforeProjection(string sql)
    {
        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        var project = (PlanNode.Project)select.Root;
        var filter = project.Source as PlanNode.Filter;

        Assert.NotNull(filter);
    }

    [Theory]
    [InlineData("SELECT * FROM t1 LIMIT 1;")]
    [InlineData("SELECT * FROM t1 WHERE x > 0 LIMIT 10;")]
    public void Plan_Select_WithLimit_PlacesLimitAsRoot(string sql)
    {
        var plan = Plan(sql);

        var select = (QueryPlan.Select)plan;
        Assert.IsType<PlanNode.Limit>(select.Root);
    }

    [Fact]
    public void Plan_Delete_WithoutWhere_CreatesTableScanPlan()
    {
        const string sql = "DELETE FROM users;";

        var plan = Plan(sql);

        Assert.IsType<QueryPlan.Delete>(plan);
        var delete = (QueryPlan.Delete)plan;
        var scan = (PlanNode.TableScan)delete.Root;
        Assert.Equal("users", scan.TableName);
        Assert.NotNull(delete.Root);
        Assert.IsType<PlanNode.TableScan>(scan);
    }

    [Fact]
    public void Plan_Delete_WithWhere_CreatesFilterPlan()
    {
        const string sql = "DELETE FROM users WHERE id = 1;";

        var plan = Plan(sql);

        var delete = (QueryPlan.Delete)plan;
        Assert.IsType<PlanNode.Filter>(delete.Root);
        var filter = (PlanNode.Filter)delete.Root!;
        Assert.IsType<PlanNode.TableScan>(filter.Source);
    }
}