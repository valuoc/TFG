
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

var initContext = new CommandContext(cancelProgram.Token);
foreach (var item in configuration.GetSection("Regions").GetChildren())
{
    await root.ProcessAsync(["region","add", item.Key, item.GetValue<string>("Url")??throw new ArgumentException("Missing region url in config.")], initContext);
    if(item.GetValue<bool>("Default", false))
    {
        await root.ProcessAsync(["region", "set", item.Key], initContext);
    }
}

await root.ProcessAsync(["user", "user1"], initContext);
await Task.Delay(2_000);
await root.ProcessAsync(["register"], initContext);
await root.ProcessAsync(["login"], initContext);
await root.ProcessAsync(["start-conversation", "waka", "waka"], initContext);
Console.WriteLine("Configuration loaded!");
Console.WriteLine();

while (!cancelProgram.IsCancellationRequested)
{
    var region = root.GlobalState.Get<string>("currentRegion") ?? string.Empty;
    var user = root.GlobalState.TryGetCurrentUser();
    var userName = user != null ? user.UserName + "@" : string.Empty;
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.Write($"{userName}{region}");
    Console.ResetColor();
    Console.Write("> ");
    var line = Console.ReadLine()?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    if(line[0] == "exit")
        break;
    try
    {
        var context = new CommandContext(cancelProgram.Token);
        var commandResult = await current.ProcessAsync(line, context);
        if(commandResult == CommandResult.Success)
        {
            if (!context.HasPrinted)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Ok!");
                Console.ResetColor();
            }
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
        Console.WriteLine(e.GetType().Name + ":" + e.Message);
        Console.WriteLine(e.StackTrace);
        Console.ResetColor();
        current = root;
    }
    Console.WriteLine();
}
Console.WriteLine("Bye!");

