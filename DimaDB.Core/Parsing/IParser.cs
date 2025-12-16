using DimaDB.Core.Lexing;

namespace DimaDB.Core.Parsing;

public interface IParser
{
    IList<Statement> Parse(string source, IList<Token> tokens);
}