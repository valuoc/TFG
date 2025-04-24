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
            var conversationLocator = command[0].Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var handle = conversationLocator[0][1..];
            var conversationId = conversationLocator[1];
            await currentUser.Client.Content.CommentAsync(handle, conversationId, string.Join(' ', command[1..]), context.Cancellation);
            var conversation = await currentUser.Client.Content.GetConversationAsync(handle, conversationId, context.Cancellation);
            Print(2, conversation, context);
            return CommandResult.Success;
        }
        return CommandResult.Incomplete;
    }
}