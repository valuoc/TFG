using SocialApp.ClientApi.Cli.Region.Operations;

namespace SocialApp.ClientApi.Cli.Region;

public sealed class RegionCommander : Commander
{
    public RegionCommander(CommanderState globalState)
        : base("region", globalState) { }

    protected override IEnumerable<Commander> GetCommanders()
        => [
            new RegionAddCommander(GlobalState), 
            new RegionDelCommander(GlobalState), 
            new RegionListCommander(GlobalState), 
            new RegionSetCommander(GlobalState)
        ];
}