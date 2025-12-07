using DimaDB.Core.Lexing;

namespace DimaDB.Core.Interfaces;

public interface ILexer
{
    IList<Token> Tokenize(string source);
}