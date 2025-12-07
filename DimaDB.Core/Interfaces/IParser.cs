using DimaDB.Core.Lexing;
using DimaDB.Core.Parsing;

namespace DimaDB.Core.Interfaces;

public interface IParser
{
    IList<Statement> Parse(IList<Token> tokens);
}