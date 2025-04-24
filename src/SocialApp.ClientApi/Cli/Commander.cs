namespace SocialApp.ClientApi.Cli;

public enum CommandResult { NotFound, Incomplete, Success }

public abstract class Commander
{
    public string Command {get; private set;}
    public CommanderState GlobalState { get; }
    private readonly Commander[] _subCommanders;

    protected virtual IEnumerable<Commander> GetCommanders()
    {
        yield break;
    }

    protected Commander(string command, CommanderState globalState)
    {
        Command = command;
        GlobalState = globalState;
        _subCommanders = GetCommanders().ToArray();
    }
    
    public virtual async Task<CommandResult> ProcessAsync(string[] command, CancellationToken cancel)
    {
        if (command.Length == 0)
        {
            return CommandResult.Incomplete;
        }
        
        foreach (var commander in _subCommanders)
        {
            if(commander.Command == command[0])
            {
                var status = await commander.ProcessAsync(command[1..], cancel);
                if(status != CommandResult.Success)
                    status = CommandResult.Incomplete;
                return status;
            }
        }
        return CommandResult.NotFound;
    }

    protected void Print(int padding, string line, ConsoleColor? color = null)
    {
        if (color.HasValue)
            Console.ForegroundColor = color.Value;
        else
            Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("|" + "".PadLeft(padding, ' ') + line);
        Console.ResetColor();
    }
}