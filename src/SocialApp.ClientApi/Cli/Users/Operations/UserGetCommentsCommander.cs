namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserGetCommentsCommander : Commander
{
    public UserGetCommentsCommander(CommanderState globalState) 
        : base("comments-before", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        if (command.Length < 2)
            return CommandResult.Incomplete;
        
        var currentUser = GlobalState.GetCurrentUserOrFail();

        var (handle, conversationId) = ParseConversationLocator(command[0]);
        var before = command[1];
        var comments = await currentUser.Client.Content.GetConversationCommentsBeforeAsync(handle, conversationId, before, context.Cancellation);
        Print(2, comments, context);
        return CommandResult.Success;
    }
}