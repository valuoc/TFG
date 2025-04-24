using SocialApp.Models.Session;

namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserLoginCommander : Commander
{
    public UserLoginCommander(CommanderState globalState) 
        : base("login", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        var currentUser = GlobalState.GetCurrentUserOrFail();
        
        await currentUser.Client.Session.LoginAsync(new LoginRequest()
        {
            Email = currentUser.Email,
            Password = currentUser.Password
        }, context.Cancellation);
        Print(2, "User logged in!", context);
        return CommandResult.Success;
    }
}