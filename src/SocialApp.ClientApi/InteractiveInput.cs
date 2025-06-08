using System.Text;
using SocialApp.ClientApi.Cli;

namespace SocialApp.ClientApi;

public static class InteractiveInput
{
    public static async Task RunAsync(RootCommander root, CancellationToken cancelProgram)
    {
        Commander? current = root;
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
                keyInfo = ProcessKey(buffer, history, ref historyIndex);
            }

            historyIndex = 0;
            var line = buffer.ToString();
            
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write($"{userName}{region}");
            Console.ResetColor();
            Console.Write($"> {line}\n");

            var repeats = 1;
            
            var macroDelimiter = line.IndexOf('%');
            if (macroDelimiter != -1)
            {
                var macro = line.Substring(0, macroDelimiter).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (macro[0] == "repeat")
                {
                    repeats = int.Parse(macro[1]);
                }
                line = line.Remove(0, macroDelimiter +1);
            }
            
            var command = line?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
            if (command is ["exit"])
                break;
            try
            {
                for(var i=0; i<repeats; i++)
                {
                    var context = new CommandContext(cancelProgram);
                    var commandResult = await current.ProcessAsync(command, context);

                    if (commandResult == CommandResult.Success)
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
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine(e.GetType().Name + ":" + e.Message);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  " + e.StackTrace);
                Console.ResetColor();
                current = root;
            }

            Console.WriteLine();
        }
    }

    private static ConsoleKeyInfo ProcessKey(StringBuilder buffer, List<string> history, ref int historyIndex)
    {
        var keyInfo = Console.ReadKey(false);
        if (keyInfo.Key == ConsoleKey.Backspace)
        {
            if (buffer.Length > 0)
                Console.Write("\b");
            Console.Write("\b");
            Console.Write(" ");
            Console.Write("\b");
            if (buffer.Length > 0)
                buffer.Length--;
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

        return keyInfo;
    }
}