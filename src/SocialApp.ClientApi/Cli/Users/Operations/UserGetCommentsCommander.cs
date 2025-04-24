namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserGetCommentsCommander : Commander
{
    public UserGetCommentsCommander(CommanderState globalState) 
        : base("comments-before", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        var currentUser = GlobalState.GetCurrentUserOrFail();

        var (handle, conversationId) = ParseConversationLocator(command);
        var before = command[2];
        var comments = await currentUser.Client.Content.GetConversationCommentsBeforeAsync(handle, conversationId, before, context.Cancellation);
        Print(2, comments, context);
        return CommandResult.Success;
    }
}