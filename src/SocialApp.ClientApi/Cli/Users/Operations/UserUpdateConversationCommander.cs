namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserUpdateConversationCommander : Commander
{
    public UserUpdateConversationCommander(CommanderState globalState) 
        : base("update-conversation", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        var currentUser = GlobalState.GetCurrentUserOrFail();

        var (handle, conversationId) = ParseConversationLocator(command);
        var content = command[2];
        await currentUser.Client.Content.UpdateConversationAsync(handle, conversationId, content, context.Cancellation);
        var conversation = await currentUser.Client.Content.GetConversationAsync(currentUser.Handle, conversationId, context.Cancellation);
        Print(2, conversation, context);
        return CommandResult.Success;
    }
}