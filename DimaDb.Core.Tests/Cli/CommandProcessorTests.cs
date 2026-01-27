using DimaDB.Core.Cli;
using DimaDB.Core.ErrorHandling;
using DimaDB.Core.Lexing;
using DimaDB.Core.Parsing;

namespace DimaDB.Core.Tests.Cli;

public class CommandProcessorTests
{
    private readonly CommandProcessor _commandProcessor;

    public CommandProcessorTests()
    {
        var errorReporter = new ErrorReporter();
        _commandProcessor = new CommandProcessor(errorReporter, new Lexer(errorReporter), new Parser(errorReporter), default);
    }

    [Fact()]
    public void EmptyQueryReturnsCode0()
    {
        var actual = _commandProcessor.Process("", false);
        Assert.Equal(0, actual);
    }

    [Fact()]
    public void CorrectQueryReturnsCode0()
    {
        var actual = _commandProcessor.Process("SELECT name FROM users;", false);
        Assert.Equal(0, actual);
    }

    [Fact()]
    public void LexerErrorReturnsCode2()
    {
        var actual = _commandProcessor.Process("SELECT $name FROM users;", false);
        Assert.Equal(2, actual);
    }

    [Fact()]
    public void ParserErrorReturnsCode3()
    {
        var actual = _commandProcessor.Process("SELECT FROM users;", false);
        Assert.Equal(3, actual);
    }
}