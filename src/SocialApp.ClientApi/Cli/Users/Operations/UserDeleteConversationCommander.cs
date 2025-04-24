namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserDeleteConversationCommander : Commander
{
    public UserDeleteConversationCommander(CommanderState globalState) 
        : base("delete-conversation", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        if (command.Length < 1)
            return CommandResult.Incomplete;
        var currentUser = GlobalState.GetCurrentUserOrFail();

        var (handle, conversationId) = ParseConversationLocator(command[0]);
        await currentUser.Client.Content.DeleteConversationAsync(handle, conversationId, context.Cancellation);
        return CommandResult.Success;
    }
}