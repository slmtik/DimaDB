using DimaDB.Core.ErrorHandling;
using DimaDB.Core.Interfaces;
using DimaDB.Core.Lexing;

namespace DimaDB.Core;

public class CommandProcessor(ErrorReporter errorReporter, ILexer lexer, IParser parser, IAstPrinter astPrinter)
{
    public int Process(string command, bool isDebug)
    {
        errorReporter.Reset();

        var tokens = lexer.Tokenize(command);
        if (tokens.Count == 0 || tokens[0].TokenType == TokenType.EoF)
        {
            return 0;
        }

        if (errorReporter.ErrorCode > 0)
        {
            return 2;
        }

        var statements = parser.Parse(tokens);
        if (errorReporter.ErrorCode > 0)
        {
            return 3;
        }

        if (isDebug)
        {
            Console.WriteLine(astPrinter.Print(statements));
        }

        return 0;
    }
}
