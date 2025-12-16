namespace DimaDB.Core.Lexing;

public interface ILexer
{
    IList<Token> Tokenize(string source);
}