using DimaDB.Core.Parsing;

namespace DimaDB.Core.Interfaces;

public interface IAstPrinter
{
    string Print(IList<Statement> statements);
}