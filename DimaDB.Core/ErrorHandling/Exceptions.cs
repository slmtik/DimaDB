using DimaDB.Core.Lexing;

namespace DimaDB.Core.ErrorHandling;

public class LexerException(int line, int position, string message) : Exception(message)
{
    public int Line { get; } = line;
    public int Position { get; } = position;
}

public class ParserException(string input, Token token, string message) : Exception(message)
{
    public string Input { get; } = input;
    public Token Token { get; } = token;
}