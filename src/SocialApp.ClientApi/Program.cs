
using System.Text;
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

var buffer = new StringBuilder(100);
var history = new List<string>(100);
var historyIndex = 0;
while (!cancelProgram.IsCancellationRequested)
{
    var region = root.GlobalState.Get<string>("currentRegion") ?? string.Empty;
    var user = root.GlobalState.TryGetCurrentUser();
    var userName = user != null ? user.UserName + "@" : string.Empty;
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.Write($"{userName}{region}");
    Console.ResetColor();
    Console.Write("> ");
    var keyInfo = new ConsoleKeyInfo();
    
    buffer.Clear();
    while (keyInfo.Key != ConsoleKey.Enter)
    {
        keyInfo = Console.ReadKey(false);
        if (keyInfo.Key == ConsoleKey.Backspace)
        {
            Console.Write("\b");
            Console.Write(" ");
            Console.Write("\b");
        }
        else if (keyInfo.Key == ConsoleKey.UpArrow)
        {
            if (historyIndex < history.Count)
            {
                historyIndex++;
                var length = buffer.Length;
                for (var i = 0; i < length; i++)
                {
                    Console.Write("\b");
                    Console.Write(" ");
                    Console.Write("\b");
                }

                buffer.Clear();
                buffer.Append(history[^historyIndex]);
                Console.Write(buffer.ToString());
            }
        }
        else if (keyInfo.Key == ConsoleKey.DownArrow)
        {
            if (historyIndex > 0)
            {
                historyIndex--;
                var length = buffer.Length;
                for (var i = 0; i < length; i++)
                {
                    Console.Write("\b");
                    Console.Write(" ");
                    Console.Write("\b");
                }

                buffer.Clear();
                if (historyIndex > 0)
                {
                    buffer.Append(history[^historyIndex]);
                    Console.Write(buffer.ToString());
                }
            }
        }
        else if (keyInfo.Key != ConsoleKey.Enter)
        {
            buffer.Append(keyInfo.KeyChar);
        }
        else if (keyInfo.Key == ConsoleKey.Enter)
        {
            history.Add(buffer.ToString());
        }
    }

    historyIndex = 0;
    var line = buffer.ToString();
    var command = line?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
    Console.ForegroundColor = ConsoleColor.Magenta;
    Console.Write($"{userName}{region}");
    Console.ResetColor();
    Console.Write($"> {line}\n");
    if(command[0] == "exit")
        break;
    try
    {
        var context = new CommandContext(cancelProgram.Token);
        var commandResult = await current.ProcessAsync(command, context);
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

