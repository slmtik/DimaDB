using DimaDB.Core.ErrorHandling;
using DimaDB.Core.Lexing;

namespace DimaDb.Core.Tests.Lexing;

public class LexerTests
{
    [Fact]
    public void Tokenize_SelectFrom_ReturnsExpectedTokens()
    {
        var lexer = new Lexer(null);
        var source = "SELECT name, age FROM users;";
        var tokens = lexer.Tokenize(source);

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

        Assert.Equal("name", tokens[1].Lexeme);
        Assert.Equal("age", tokens[3].Lexeme);
        Assert.Equal("users", tokens[5].Lexeme);
    }

    [Fact]
    public void Tokenize_StringAndNumberLiteralsAndQuotedIdentifier()
    {
        var lexer = new Lexer(null);
        var source = "INSERT INTO \"MyTable\" VALUES (123, 'hello');";
        var tokens = lexer.Tokenize(source);

        Assert.Contains(tokens, t => t.TokenType == TokenType.NumberLiteral && (double)t.Literal! == 123);
        Assert.Contains(tokens, t => t.TokenType == TokenType.StringLiteral && (string)t.Literal! == "hello");
        
        var quoted = tokens.FirstOrDefault(t => t.TokenType == TokenType.Identifier && t.Quoted);
        Assert.NotNull(quoted);
        Assert.Equal("MyTable", quoted!.Lexeme);
    }

    [Fact]
    public void Tokenize_CreateTable_ReturnsExpectedTokens()
    {
        var lexer = new Lexer(null);
        var source = "CREATE TABLE users (id INT, name TEXT);";
        var tokens = lexer.Tokenize(source).ToArray();

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

        Assert.Equal("users", tokens[2].Lexeme);
        Assert.Equal("id", tokens[4].Lexeme);
        Assert.Equal("name", tokens[7].Lexeme);
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
}