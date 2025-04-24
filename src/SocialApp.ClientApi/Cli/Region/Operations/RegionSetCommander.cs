namespace SocialApp.ClientApi.Cli.Region.Operations;

public sealed class RegionSetCommander : Commander
{
    public RegionSetCommander(CommanderState globalState)
        : base("set", globalState) { }

    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        if (command.Length == 1)
        {
            var items = GlobalState.GetMany<string>("regions");
            if (items.ContainsKey(command[0]))
            {
                GlobalState.Set(command[0], "currentRegion");
                Print(2, $"Region '{command[0]}' is current region.", context);
                return CommandResult.Success;
            }
            Print(2, $"Region '{command[0]}' not found.", context, ConsoleColor.DarkRed);
        }
        return CommandResult.Incomplete;
    }
}