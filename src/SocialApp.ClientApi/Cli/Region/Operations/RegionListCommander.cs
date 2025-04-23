namespace SocialApp.ClientApi.Cli.Region.Operations;

public sealed class RegionListCommander : Commander
{
    public RegionListCommander(CommanderState globalState)
        : base("list", globalState) { }

    public override async Task<CommandResult> ProcessAsync(string[] command, CancellationToken cancel)
    {
        var items = GlobalState.GetMany<string>("regions");
        foreach (var item in items)
            Console.WriteLine($" - {item.Key}: {item.Value}");
        
        return CommandResult.Success;
    }
}