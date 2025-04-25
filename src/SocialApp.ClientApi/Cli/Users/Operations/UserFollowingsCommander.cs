namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserFollowingsCommander : Commander
{
    public UserFollowingsCommander(CommanderState globalState) 
        : base("followings", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        var currentUser = GlobalState.GetCurrentUserOrFail();
        var followings = await currentUser.Client.Follow.GetFollowingsAsync(context.Cancellation);
        Print(2, followings, context);
        return CommandResult.Success;
    }
}