namespace SocialApp.ClientApi.Cli.Configuration;

public class GetConfigCommander : Commander
{
    public GetConfigCommander(CommanderState globalState) 
        : base("get", globalState)
    {
    }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        if (command.Length == 1)
        {
            var value = GlobalState.Get<string>("config", command[0]);
            Print(2, value, context);
            return CommandResult.Success;
        }

        return CommandResult.Incomplete;
    }
}