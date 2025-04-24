namespace SocialApp.ClientApi.Cli.Region.Operations;

public sealed class RegionListCommander : Commander
{
    public RegionListCommander(CommanderState globalState)
        : base("list", globalState) { }

    public override async Task<CommandResult> ProcessAsync(string[] command, CancellationToken cancel)
    {
        var regions = GlobalState.GetMany<string>("regions");
        foreach (var item in regions)
            Print(2, $" - {item.Key}: {item.Value}");
        
        return CommandResult.Success;
    }
}