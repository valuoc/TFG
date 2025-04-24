namespace SocialApp.ClientApi.Cli.Configuration;

public class ConfigCommander : Commander
{
    public ConfigCommander(CommanderState globalState) 
        : base("config", globalState) { }

    protected override IEnumerable<Commander> GetCommanders()
        => [new SetConfigCommander(GlobalState), new GetConfigCommander(GlobalState)];
}