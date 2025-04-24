using SocialApp.Models.Content;

namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserGetConversationCommander : Commander
{
    public UserGetConversationCommander(CommanderState globalState) 
        : base("get-conversation", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        var currentUser = GlobalState.GetCurrentUserOrFail();

        var (handle, conversationId) = ParseConversationLocator(command);
        var conversation = await currentUser.Client.Content.GetConversationAsync(handle, conversationId, context.Cancellation);
        Print(2, conversation, context);
        return CommandResult.Success;
    }
}