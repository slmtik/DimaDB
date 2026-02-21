using DimaDB.Core.Cli;
using DimaDB.Core.ErrorHandling;
using DimaDB.Core.Lexing;
using DimaDB.Core.Parsing;
using DimaDB.Core.Storage;

namespace DimaDB.Core.Tests.Cli;

public class CommandProcessorTests : IDisposable
{
    private readonly CommandProcessor _commandProcessor;
    private readonly string _testDir;
    private readonly StorageEngine _engine;

    public CommandProcessorTests()
    {
        var errorReporter = new ErrorReporter();
        _testDir = Path.Combine(Path.GetTempPath(), $"dimadb_test_{Guid.NewGuid()}");
        _engine = new StorageEngine(_testDir);
        _commandProcessor = new CommandProcessor(errorReporter, _engine, new Lexer(errorReporter), new Parser(errorReporter), default!);
    }

    [Fact()]
    public void EmptyQueryReturnsCode0()
    {
        var actual = _commandProcessor.Process("", false);
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

    [Fact]
    public void CreateTable_CreatesTableSuccessfully()
    {
        var exitCode = _commandProcessor.Process("CREATE TABLE test (id INT, name TEXT);", false);
        
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void InsertInto_InsertsRecordSuccessfully()
    {
        _commandProcessor.Process("CREATE TABLE test (id INT, name TEXT);", false);
        var exitCode = _commandProcessor.Process("INSERT INTO test VALUES (1, 'Alice');", false);

        Assert.Equal(0, exitCode);
    }

    public void Dispose()
    {
        _engine?.Dispose();
        GC.SuppressFinalize(this);

        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch { }
        }
    }
}