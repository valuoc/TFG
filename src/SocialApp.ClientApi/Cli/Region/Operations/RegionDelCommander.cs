namespace SocialApp.ClientApi.Cli.Region.Operations;

public sealed class RegionDelCommander : Commander
{
    public RegionDelCommander(CommanderState globalState)
        : base("del", globalState) { }

    public override async Task<CommandResult> ProcessAsync(string[] command, CancellationToken cancel)
    {
        if (command.Length == 1)
        {
            GlobalState.Remove("regions", command[0]);
            return CommandResult.Success;
        }
        return CommandResult.Incomplete;
    }
}