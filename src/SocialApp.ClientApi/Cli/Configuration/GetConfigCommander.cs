namespace SocialApp.ClientApi.Cli.Configuration;

public class GetConfigCommander : Commander
{
    public GetConfigCommander(CommanderState globalState) 
        : base("get", globalState)
    {
    }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CancellationToken cancel)
    {
        if (command.Length == 1)
        {
            var value = GlobalState.Get<string>("config", command[0]);
            Print(2, value);
            return CommandResult.Success;
        }

        return CommandResult.Incomplete;
    }
}