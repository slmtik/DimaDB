using DimaDB.Core.ErrorHandling;
using DimaDB.Core.Lexing;
using DimaDB.Core.Parsing;
using DimaDB.Core.Printing;
using DimaDB.Core.Storage;
using DimaDB.Core.Storage.Types;

namespace DimaDB.Core.Cli;

public class CommandProcessor(ErrorReporter errorReporter, StorageEngine storageEngine, ILexer lexer, IParser parser, IAstPrinter astPrinter)
{
    public int Process(string command, bool isDebug)
    {
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
                ExecuteStatement(statement);
                storageEngine.Flush();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Execution error: {ex.Message}");
            return 4;
        }

        return 0;
    }

    private void ExecuteStatement(Statement statement)
    {
        if (statement is Statement.CreateTable createTable)
        {
            ExecuteCreateTable(createTable);
        }
        else if (statement is Statement.InsertInto insertInto)
        {
            ExecuteInsertInto(insertInto);
        }
        else if (statement is Statement.Select select)
        {
            ExecuteSelect(select);
        }
    }

    private void ExecuteCreateTable(Statement.CreateTable createTable)
    {
        var columns = createTable.ColumnDefinitions.Select(cd =>
            new ColumnDefinition(
                cd.Column.Name,
                ColumnTypeExtensions.FromString(cd.Type.Type),
                true
            )
        ).ToArray();

        storageEngine.CreateTable(createTable.Table.Name, columns);
        Console.WriteLine($"Table '{createTable.Table.Name}' created successfully.");
    }

    private void ExecuteInsertInto(Statement.InsertInto insertInto)
    {
        var table = storageEngine.OpenTable(insertInto.Table.Name);

        var values = insertInto.Expressions.Select(EvaluateExpression).ToArray();
        var rid = table.Insert(values);

        Console.WriteLine($"Inserted record into '{insertInto.Table.Name}' with RID {rid}");
    }

    private void ExecuteSelect(Statement.Select select)
    {
        var table = storageEngine.OpenTable(select.FromClause.TableRefence.Table.Name);

        int rowCount = 0;
        foreach (var (rid, record) in table.Scan())
        {
            Console.WriteLine(string.Join(" | ", record.Select(v => v?.ToString() ?? "NULL")));
            rowCount++;
        }

        if (rowCount == 0)
        {
            Console.WriteLine("(0 rows)");
        }
        else
        {
            Console.WriteLine($"({rowCount} rows)");
        }
    }

    private object? EvaluateExpression(Expression expr)
    {
        return expr switch
        {
            Expression.NumberLiteral numLit => numLit.Value,
            Expression.StringLiteral strLit => strLit.Value,
            Expression.BooleanLiteral boolLit => boolLit.Value,
            Expression.NullLiteral => null,
            _ => throw new NotImplementedException($"Expression type {expr.GetType().Name} not supported")
        };
    }
}
