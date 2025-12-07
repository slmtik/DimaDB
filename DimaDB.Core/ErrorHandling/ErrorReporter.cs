using DimaDB.Core.Lexing;

namespace DimaDB.Core.ErrorHandling;

public class ErrorReporter
{
    private int _errorCode = 0;

    public int ErrorCode => _errorCode;

    public void Reset()
    {
        _errorCode = 0;
    }

    public void Report(LexerException exception)
    {
        Console.Error.WriteLine($"[Line {exception.Line}, Position {exception.Position}] Lexer Error: {exception.Message}");

        _errorCode = 2;
    }

    public void Report(ParserException exception)
    {
        if (exception.Token.TokenType == TokenType.EoF)
        {
            Console.Error.WriteLine($"[Line {exception.Token.Line}, Position {exception.Token.Position}] Parser Error at end: {exception.Message}");
        }
        else
        {
            Console.Error.WriteLine($"[Line {exception.Token.Line}, Position {exception.Token.Position}] Parser Error at '{exception.Token.Lexeme}': {exception.Message}");
        }

        _errorCode = 3;
    }
}
