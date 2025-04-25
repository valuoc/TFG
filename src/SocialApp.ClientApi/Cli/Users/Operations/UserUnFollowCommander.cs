namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserUnFollowCommander : Commander
{
    public UserUnFollowCommander(CommanderState globalState) 
        : base("unfollow", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        if (command.Length < 1)
            return CommandResult.Incomplete;
        
        var currentUser = GlobalState.GetCurrentUserOrFail();

        var handle = command[0][1..];
        await currentUser.Client.Follow.RemoveAsync(handle, context.Cancellation);
        var followings = await currentUser.Client.Follow.GetFollowingsAsync(context.Cancellation);
        Print(2, followings, context);
        return CommandResult.Success;
    }
}