using DimaDB.Core.Lexing;

namespace DimaDB.Core.ErrorHandling;

public class ErrorReporter
{
    private int _errorCode = 0;

    public int ErrorCode => _errorCode;

    public void Report(LexerException exception)
    {
        Console.Error.WriteLine($"[Line {exception.Line}, Position {exception.Position}] Lexer Error: {exception.Message}");

        _errorCode = 2;
    }

    public void Report(ParserException exception)
    {
        if (exception.Token.TokenType == TokenType.EoF)
        {
            Console.Error.WriteLine($"[Line {exception.Token.Line}, Position {exception.Token.Start}] Parser Error at end: {exception.Message}");
        }
        else
        {
            var lexeme = exception.Input.AsSpan(exception.Token.Start, exception.Token.Length);
            Console.Error.WriteLine($"[Line {exception.Token.Line}, Position {exception.Token.Start}] Parser Error at '{lexeme}': {exception.Message}");
        }

        _errorCode = 3;
    }
}
