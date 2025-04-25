namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserFollowersCommander : Commander
{
    public UserFollowersCommander(CommanderState globalState) 
        : base("followers", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        var currentUser = GlobalState.GetCurrentUserOrFail();
        var followings = await currentUser.Client.Follow.GetFollowersAsync(context.Cancellation);
        Print(2, followings, context);
        return CommandResult.Success;
    }
}