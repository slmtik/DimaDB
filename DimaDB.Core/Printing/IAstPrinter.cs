using DimaDB.Core.Parsing;

namespace DimaDB.Core.Printing;

public interface IAstPrinter
{
    string Print(IList<Statement> statements);
}