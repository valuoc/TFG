using SocialApp.Models.Account;

namespace SocialApp.ClientApi.Cli.Users.Operations;

public class UserRegisterCommander : Commander
{
    public UserRegisterCommander(CommanderState globalState) 
        : base("register", globalState)
    {
    }

    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        var currentUser = GlobalState.GetCurrentUserOrFail();
        
        await currentUser.Client.Account.RegisterAsync(new RegisterRequest
        {
            Email = currentUser.Email,
            Password = currentUser.Password,
            Handle = currentUser.Handle,
            DisplayName = currentUser.UserName
        }, context.Cancellation);
        Print(2, "User registerd!", context);
        return CommandResult.Success;
    }
}