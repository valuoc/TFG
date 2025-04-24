using SocialApp.ClientApi.Cli.Configuration;
using SocialApp.ClientApi.Cli.Region;
using SocialApp.ClientApi.Cli.Users;
using SocialApp.ClientApi.Cli.Users.Operations;

namespace SocialApp.ClientApi.Cli;

public sealed class RootCommander : Commander
{
    public RootCommander()
        :base(string.Empty, new CommanderState()) { }
    
    protected override IEnumerable<Commander> GetCommanders()
        => 
        [
            new RegionCommander(GlobalState), 
            new ConfigCommander(GlobalState), 
            new UserCommander(GlobalState),
            new UserRegisterCommander(GlobalState)
        ];
}