using SocialApp.ClientApi.Cli.Region;

namespace SocialApp.ClientApi.Cli;

public sealed class RootCommander : Commander
{
    public RootCommander()
        :base(string.Empty, new CommanderState()) { }
    
    protected override IEnumerable<Commander> GetCommanders()
        => [new RegionCommander(GlobalState)];
}