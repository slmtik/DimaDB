using ConsoleTables;
using DimaDB.ErrorHandling;
using DimaDB.Execution;
using DimaDB.Lexing;
using DimaDB.Parsing;
using DimaDB.Planning;
using DimaDB.Printing;
using DimaDB.Storage;

namespace DimaDB.Cli;

public class CommandProcessor(ErrorReporter errorReporter, StorageEngine storageEngine, Lexer lexer, Parser parser, AstPrinter astPrinter)
{
    private readonly QueryPlanner _planner = new();
    private readonly QueryExecutor _executor = new(storageEngine);

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

        var statements = parser.Parse(command, tokens);
        if (errorReporter.ErrorCode > 0)
        {
            return 3;
        }

        if (isDebug)
        {
            Console.WriteLine(astPrinter.Print(statements));
        }

        try
        {
            foreach (var statement in statements)
            {
                var plan = _planner.Plan(statement);
                var result = _executor.Execute(plan);
                PrintResult(result);
            }
            storageEngine.Flush();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Execution error: {ex.Message}");
            return 4;
        }

        return 0;
    }

    private static void PrintResult(ExecutionResult result)
    {
        switch (result)
        {
            case ExecutionResult.Select select:
                PrintSelectResult(select);
                break;

            case ExecutionResult.CreateTable ct:
                Console.WriteLine(ct.Message);
                break;

            case ExecutionResult.InsertInto ii:
                Console.WriteLine(ii.Message);
                break;
        }
    }

    private static void PrintSelectResult(ExecutionResult.Select select)
    {
        var table = new ConsoleTable([.. select.ColumnNames]);
        foreach (var row in select.Rows)
        {
            table.AddRow([.. row.Select(v => v?.ToString() ?? "NULL")]);
        }

        table.Write();
    }
}
