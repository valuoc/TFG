namespace SocialApp.ClientApi.Cli.Region.Operations;

public sealed class RegionAddCommander : Commander
{
    public RegionAddCommander(CommanderState globalState)
        : base("add", globalState) { }

    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        if (command.Length == 2)
        {
            if (!Uri.TryCreate(command[1], UriKind.Absolute, out var uri))
                throw new ArgumentException("Invalid command");
            
            GlobalState.Set(command[1], "regions", command[0]);
            Print(2, $"Region added '{command[0]}':'{command[1]}'.", context);
            return CommandResult.Success;
        }
        return CommandResult.Incomplete;
    }
}