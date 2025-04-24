namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserStartConversationCommander : Commander
{
    public UserStartConversationCommander(CommanderState globalState) 
        : base("start-conversation", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        var currentUser = GlobalState.GetCurrentUserOrFail();

        var conversationId = await currentUser.Client.Content.StartConversationAsync(string.Join(' ', command), context.Cancellation);
        var conversation = await currentUser.Client.Content.GetConversationAsync(currentUser.Handle, conversationId, context.Cancellation);
        Print(2, conversation, context);
        return CommandResult.Success;
    }
}