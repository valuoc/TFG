namespace SocialApp.ClientApi.Cli.Configuration;

public class SetConfigCommander : Commander
{
    public SetConfigCommander(CommanderState globalState) 
        : base("set", globalState) { }

    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        if (command.Length == 2)
        {
            GlobalState.Set(command[1], "config", command[0]);
            return CommandResult.Success;
        }

        return CommandResult.Incomplete;
    }
}