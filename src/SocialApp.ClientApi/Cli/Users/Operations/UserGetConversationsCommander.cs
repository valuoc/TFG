namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserGetConversationsCommander : Commander
{
    public UserGetConversationsCommander(CommanderState globalState) 
        : base("conversations", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        if (command.Length < 1)
            return CommandResult.Incomplete;
        
        var currentUser = GlobalState.GetCurrentUserOrFail();

        var handle = command[0][1..];
        var before = command.Length > 1 ? command[1] : string.Empty;
        var conversations = await currentUser.Client.Content.GetConversationsAsync(handle, before, context.Cancellation);
        Print(2, conversations, context);
        return CommandResult.Success;
    }
}