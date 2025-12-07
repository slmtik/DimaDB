using DimaDB.Core.ErrorHandling;
using DimaDB.Core.Lexing;
using DimaDB.Core.Parsing;

namespace DimaDb.Core.Tests.Parsing;

public class ParserTests
{
    [Fact]
    public void Parse_SelectWithWhereAndLimit_ParsesCorrectly()
    {
        var lexer = new Lexer(null);
        var parser = new Parser(null);

        var sql = "SELECT name, age FROM users WHERE age > 30 LIMIT 10;";
        var tokens = lexer.Tokenize(sql);

        var statements = parser.Parse(tokens);
        Assert.Single(statements);

        var select = Assert.IsType<Statement.Select>(statements[0]);

        Assert.Equal(2, select.SelectItems.Length);

        var firstSelectItem = select.SelectItems[0].Expression;
        Assert.IsType<Expression.ColumnReference>(firstSelectItem);

        var firstColumn = (Expression.ColumnReference)firstSelectItem;
        Assert.Equal("name", firstColumn.Column.Name);

        var secondSelectItem = select.SelectItems[1].Expression;
        Assert.IsType<Expression.ColumnReference>(secondSelectItem);

        var secondColumn = (Expression.ColumnReference)secondSelectItem;
        Assert.Equal("age", secondColumn.Column.Name);

        Assert.NotNull(select.FromClause);
        Assert.Equal("users", select.FromClause!.TableRefence.Table.Name);

        Assert.NotNull(select.WhereClause);
        var where = Assert.IsType<Expression.BinaryOperation>(select.WhereClause);

        var whereLeft = Assert.IsType<Expression.ColumnReference>(where.LeftOperand);
        Assert.Equal("age", whereLeft.Column.Name);

        var whereRight = Assert.IsType<Expression.NumberLiteral>(where.RightOperand);
        Assert.Equal(30.0, whereRight.Value);

        // LIMIT
        Assert.Equal(10L, select.Limit);
    }

    [Fact]
    public void Parse_SelectStarAndQualifiedStar_ParsesCorrectly()
    {
        var lexer = new Lexer(null);
        var parser = new Parser(null);

        var sql = "SELECT *, users.* FROM users;";
        var tokens = lexer.Tokenize(sql);

        var statements = parser.Parse(tokens);
        Assert.Single(statements);

        var select = Assert.IsType<Statement.Select>(statements[0]);
        Assert.Equal(2, select.SelectItems.Length);

        Assert.IsType<Expression.Star>(select.SelectItems[0].Expression);
        var qualifiedStar = Assert.IsType<Expression.QualifiedStar>(select.SelectItems[1].Expression);
        Assert.Equal("users", qualifiedStar.Table.Name);
    }

    [Fact]
    public void Parse_CreateTable_ParsesCorrectly()
    {
        var lexer = new Lexer(null);
        var parser = new Parser(null);

        var sql = "CREATE TABLE users (id INT, name TEXT);";
        var tokens = lexer.Tokenize(sql);

        var statements = parser.Parse(tokens);
        Assert.Single(statements);

        var create = Assert.IsType<Statement.CreateTable>(statements[0]);

        Assert.Equal("users", create.Table.Name);
        Assert.Equal(2, create.ColumnDefinitions.Length);

        var idColumn = create.ColumnDefinitions[0];
        Assert.Equal("id", idColumn.Column.Name);
        Assert.Equal("INT", idColumn.Type.Type);

        var nameColumn = create.ColumnDefinitions[1];
        Assert.Equal("name", nameColumn.Column.Name);
        Assert.Equal("TEXT", nameColumn.Type.Type);
    }

    [Fact]
    public void Parse_InsertInto_ParsesCorrectly()
    {
        var lexer = new Lexer(null);
        var parser = new Parser(null);

        var sql = "INSERT INTO users VALUES (1, 'abc');";
        var tokens = lexer.Tokenize(sql);

        var statements = parser.Parse(tokens);
        Assert.Single(statements);

        var insert = Assert.IsType<Statement.InsertInto>(statements[0]);
        Assert.Equal("users", insert.Table.Name);
        Assert.Equal(2, insert.Expressions.Length);

        var first = Assert.IsType<Expression.NumberLiteral>(insert.Expressions[0]);
        Assert.Equal(1.0, first.Value);

        var second = Assert.IsType<Expression.StringLiteral>(insert.Expressions[1]);
        Assert.Equal("abc", second.Value);
    }

    [Fact]
    public void Parse_CreateTable_WithUnsupportedType_ReportsErrorAndRecovers()
    {
        var lexer = new Lexer(null);
        var errorReporter = new ErrorReporter();
        var parser = new Parser(errorReporter);

        var sql = "CREATE TABLE users (id UNKNOWNTYPE, name TEXT);";
        var tokens = lexer.Tokenize(sql);

        var originalError = Console.Error;
        try
        {
            using var sw = new StringWriter();
            Console.SetError(sw);

            var statements = parser.Parse(tokens);

            var output = sw.ToString();
            Assert.Contains("Parser Error", output, System.StringComparison.OrdinalIgnoreCase);
            Assert.True(errorReporter.ErrorCode > 0);

            Assert.NotNull(statements);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}