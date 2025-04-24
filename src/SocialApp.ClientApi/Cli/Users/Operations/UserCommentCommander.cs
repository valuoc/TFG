namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserCommentCommander : Commander
{
    public UserCommentCommander(CommanderState globalState) 
        : base("comment", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        if (command.Length >= 2)
        {
            var currentUser = GlobalState.GetCurrentUserOrFail();
            var (handle, conversationId) = ParseConversationLocator(command);
            await currentUser.Client.Content.CommentAsync(handle, conversationId, string.Join(' ', command[1..]), context.Cancellation);
            var conversation = await currentUser.Client.Content.GetConversationAsync(handle, conversationId, context.Cancellation);
            Print(2, conversation, context);
            return CommandResult.Success;
        }
        return CommandResult.Incomplete;
    }
}