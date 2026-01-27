using DimaDB.Core.Cli;
using DimaDB.Core.ErrorHandling;
using DimaDB.Core.Lexing;
using DimaDB.Core.Parsing;
using DimaDB.Core.Printing;
using DimaDB.Repl;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Parsing;

var serviceCollection = new ServiceCollection();

serviceCollection.AddSingleton<ReplEngine>();
serviceCollection.AddSingleton<ErrorReporter>();
serviceCollection.AddTransient<CommandProcessor>();
serviceCollection.AddTransient<ILexer, Lexer>();
serviceCollection.AddTransient<IParser, Parser>();
serviceCollection.AddTransient<IAstPrinter, AstPrinter>();

var serviceProvider = serviceCollection.BuildServiceProvider();

var rootCommand = new RootCommand();
var debugOption = new Option<bool>("--ast-debug");
rootCommand.Options.Add(debugOption);

var queryOption = new Option<string>("--query");
rootCommand.Options.Add(queryOption);

ParseResult parseResult = rootCommand.Parse(args);
if (parseResult.Errors.Count > 0)
{
    foreach (ParseError parseError in parseResult.Errors)
    {
        Console.Error.WriteLine(parseError.Message);
    }

    return 1;
}

bool isDebug = parseResult.GetValue(debugOption);
string query = parseResult.GetValue(queryOption) ?? "";

if (string.IsNullOrEmpty(query))
{
    var replEngine = serviceProvider.GetRequiredService<ReplEngine>();
    await replEngine.Start(isDebug);
}
else
{
    var commandProcessor = serviceProvider.GetRequiredService<CommandProcessor>();
    return commandProcessor.Process(query, isDebug);
}

return 0;