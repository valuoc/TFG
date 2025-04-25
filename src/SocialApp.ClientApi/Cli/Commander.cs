using SocialApp.Models.Content;

namespace SocialApp.ClientApi.Cli;

public enum CommandResult { NotFound, Incomplete, Success }

public sealed class CommandContext
{
    public CancellationToken Cancellation { get; }

    public bool HasPrinted { get; set; }

    public CommandContext(CancellationToken cancellation)
    {
        Cancellation = cancellation;
    }
}

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
    
    public virtual async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        if (command.Length == 0)
        {
            return CommandResult.Incomplete;
        }
        
        foreach (var commander in _subCommanders)
        {
            if(commander.Command == command[0])
            {
                var status = await commander.ProcessAsync(command[1..], context);
                if(status != CommandResult.Success)
                    status = CommandResult.Incomplete;
                return status;
            }
        }
        return CommandResult.NotFound;
    }

    protected void Print(int padding, string line, CommandContext context, ConsoleColor? color = null)
    {
        context.HasPrinted = true;
        if (color.HasValue)
            Console.ForegroundColor = color.Value;
        else
            Console.ForegroundColor = ConsoleColor.Green;
        var pad = "".PadLeft(padding, ' ');
        Console.WriteLine("|" + pad + line);
        Console.ResetColor();
    }

    protected void Print(int padding, Conversation conversation, CommandContext context)
    {
        Print(padding, conversation.Root, context);
        Print(padding, conversation.LastComments, context);
    }

    protected void Print(int padding, ConversationRoot conversation, CommandContext context)
    {
        context.HasPrinted = true;
        var pad = "".PadLeft(padding, ' ');
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"|{pad} *  @{conversation.Handle}:{conversation.ConversationId}");
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"|{pad}      $'{conversation.Content}'");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"|{pad}      #{conversation.LastModify:HH:mm:ss}  C:{conversation.CommentCount}  L:{conversation.LikeCount}  V:{conversation.ViewCount}");
    }

    protected void Print(int padding, IReadOnlyList<ConversationRoot> conversations, CommandContext context)
    {
        foreach (var conversation in conversations)
        {
            context.HasPrinted = true;
            Print(padding, conversation, context);
        }
    }
    
    protected void Print(int padding, IReadOnlyList<ConversationComment> comments, CommandContext context)
    {
        var pad = "".PadLeft(padding, ' ');
        foreach (var comment in comments)
        {
            context.HasPrinted = true;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"|{pad}     -> @{comment.Handle}:{comment.CommentId}");
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine($"|{pad}          $'{comment.Content}'");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"|{pad}          #{comment.LastModify:HH:mm:ss}  C:{comment.CommentCount}  L:{comment.LikeCount}  V:{comment.ViewCount}");
        }
        Console.ResetColor();
    }
    
    protected void Print(int padding, IReadOnlyList<string> list, CommandContext context)
    {
        var pad = "".PadLeft(padding, ' ');
        foreach (var line in list.Chunk(3))
        {
            context.HasPrinted = true;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"|{pad}  ");
            foreach (var column in line)
            {
                Console.Write($"@{column}\t\t");
            }
            Console.Write("\n");
        }
    }
    
    protected (string handle, string conversationId) ParseConversationLocator(string command)
    {
        var conversationLocator = command.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var handle = conversationLocator[0][1..];
        var conversationId = conversationLocator[1];
        return (handle, conversationId);
    }
}