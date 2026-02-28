using DimaDB.Cli;
using Microsoft.Extensions.DependencyInjection;

namespace DimaDB.Repl;

public class ReplEngine(IServiceProvider serviceProvider)
{
    public Task Start(bool isDebug, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Welcome to DimaDB REPL!");
        Console.WriteLine("Type 'exit' to quit.");

        var commandProcessor = serviceProvider.GetRequiredService<CommandProcessor>();
        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("DimaDB> ");
            var input = Console.ReadLine();
            if (input == null || input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            commandProcessor.Process(input, isDebug);
        }

        Console.WriteLine("Exiting DimaDB REPL. Goodbye!");
        return Task.CompletedTask;
    }
}
