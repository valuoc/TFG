
using Microsoft.Extensions.Configuration;
using SocialApp.ClientApi.Cli;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Local.json", optional: true)
    .Build();

var cancelProgram = new CancellationTokenSource();

Console.CancelKeyPress += (_, _) => cancelProgram.Cancel();

var root = new RootCommander();
Commander? current = root;
while (!cancelProgram.IsCancellationRequested)
{
    Console.Write($"{current.Prompt}> ");
    var line = Console.ReadLine()?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    if(line[0] == "exit")
        break;
    try
    {
        var commandResult = await current.ProcessAsync(line, cancel: cancelProgram.Token);
        if(commandResult == CommandResult.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Ok!");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"Cannot execute command! {commandResult}");
            Console.ResetColor();
        }
    }
    catch (Exception e)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(e.Message);
        Console.ResetColor();
        current = root;
    }
}
Console.WriteLine("Bye!");

