namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserUnLikeCommander : Commander
{
    public UserUnLikeCommander(CommanderState globalState) 
        : base("unlike", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        if (command.Length < 1)
            return CommandResult.Incomplete;
        
        var currentUser = GlobalState.GetCurrentUserOrFail();

        var (handle, conversationId) = ParseConversationLocator(command[0]);
        await currentUser.Client.Content.ReactToConversationAsync(handle, conversationId, false, context.Cancellation);
        var conversation = await currentUser.Client.Content.GetConversationAsync(currentUser.Handle, conversationId, context.Cancellation);
        Print(2, conversation, context);
        return CommandResult.Success;
    }
}