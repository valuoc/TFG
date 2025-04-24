namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserFeedCommander : Commander
{
    public UserFeedCommander(CommanderState globalState) 
        : base("feed", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        var currentUser = GlobalState.GetCurrentUserOrFail();
        
        var before = command.Length > 0 ? command[0] : string.Empty;
        var conversations = await currentUser.Client.Feed.FeedAsync(before, context.Cancellation);
        Print(2, conversations, context);
        return CommandResult.Success;
    }
}