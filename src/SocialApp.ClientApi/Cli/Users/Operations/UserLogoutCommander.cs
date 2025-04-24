namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserLogoutCommander : Commander
{
    public UserLogoutCommander(CommanderState globalState) 
        : base("logout", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        var currentUser = GlobalState.GetCurrentUserOrFail();
        
        await currentUser.Client.Session.LogoutAsync(context.Cancellation);
        Print(2, "User logged out!", context);
        return CommandResult.Success;
    }
}