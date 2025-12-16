using DimaDB.Core.ErrorHandling;
using DimaDB.Core.Lexing;

namespace DimaDb.Core.Tests.Lexing;

public class LexerTests
{
    private readonly ILexer _lexer;

    public LexerTests()
    {
        _lexer = new Lexer();
    }

    [Fact]
    public void Tokenize_SelectFrom_ReturnsExpectedTokens()
    {
        var source = "SELECT name, age FROM users;";
        var tokens = _lexer.Tokenize(source);

        var expectedTypes = new[]
        {
            TokenType.Select,
            TokenType.Identifier,
            TokenType.Comma,
            TokenType.Identifier,
            TokenType.From,
            TokenType.Identifier,
            TokenType.Semicolon,
            TokenType.EoF
        };

        Assert.Equal(expectedTypes.Length, tokens.Count);

        for (int i = 0; i < expectedTypes.Length; i++)
        {
            Assert.Equal(expectedTypes[i], tokens[i].TokenType);
        }

        Assert.Equal("name", source.AsSpan(tokens[1].Start, tokens[1].Length).ToString());
        Assert.Equal("age", source.AsSpan(tokens[3].Start, tokens[3].Length).ToString());
        Assert.Equal("users", source.AsSpan(tokens[5].Start, tokens[5].Length).ToString());
    }

    [Fact]
    public void Tokenize_StringAndNumberLiteralsAndQuotedIdentifier()
    {
        var source = "INSERT INTO \"MyTable\" VALUES (123, 'hello');";
        var tokens = _lexer.Tokenize(source);

        Assert.Contains(tokens, t => t.TokenType == TokenType.NumberLiteral && double.Parse(source.AsSpan(t.Start, t.Length)) == 123);
        Assert.Contains(tokens, t => t.TokenType == TokenType.StringLiteral && source.AsSpan(t.Start, t.Length).ToString() == "'hello'");
        
        var quoted = tokens.FirstOrDefault(t => t.TokenType == TokenType.Identifier);
        Assert.NotNull(quoted);
        Assert.Equal("\"MyTable\"", source.AsSpan(quoted!.Start, quoted!.Length).ToString());
    }

    [Fact]
    public void Tokenize_CreateTable_ReturnsExpectedTokens()
    {
        var source = "CREATE TABLE users (id INT, name TEXT);";
        var tokens = _lexer.Tokenize(source).ToArray();

        var expectedTypes = new[]
        {
            TokenType.Create,
            TokenType.Table,
            TokenType.Identifier,      // users
            TokenType.LeftParenthesis,
            TokenType.Identifier,      // id
            TokenType.Int,             // INT
            TokenType.Comma,
            TokenType.Identifier,      // name
            TokenType.Text,            // TEXT
            TokenType.RightParenthesis,
            TokenType.Semicolon,
            TokenType.EoF
        };

        Assert.Equal(expectedTypes.Length, tokens.Length);

        for (int i = 0; i < expectedTypes.Length; i++)
        {
            Assert.Equal(expectedTypes[i], tokens[i].TokenType);
        }

        Assert.Equal("users", source.AsSpan(tokens[2].Start, tokens[2].Length).ToString());
        Assert.Equal("id", source.AsSpan(tokens[4].Start, tokens[4].Length).ToString());
        Assert.Equal("name", source.AsSpan(tokens[7].Start, tokens[7].Length).ToString());
    }

    [Fact]
    public void Tokenize_UnexpectedCharacter_WritesLexerErrorToConsoleError()
    {
        var errorReporter = new ErrorReporter();
        var lexer = new Lexer(errorReporter);

        var source = "SELECT name FROM users; @";
        var errOut = Console.Error;
        try
        {
            using var sw = new StringWriter();
            Console.SetError(sw);

            var tokens = lexer.Tokenize(source);

            var errorOutput = sw.ToString();
            Assert.Contains("Lexer Error", errorOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Unexpected character", errorOutput, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("@", errorOutput);

            Assert.NotEmpty(tokens);
            Assert.Equal(TokenType.EoF, tokens[^1].TokenType);
        }
        finally
        {
            Console.SetError(errOut);
        }
    }

    [Fact]
    public void Tokenize_StringLiteralWithEscapedQuote()
    {
        var source = "SELECT 'It''s raining';";
        var tokens = _lexer.Tokenize(source);

        Assert.Contains(tokens, t => t.TokenType == TokenType.StringLiteral && source.AsSpan(t.Start, t.Length).ToString() == "'It''s raining'");
    }
}