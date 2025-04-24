using System.Runtime.Serialization;
using System.Text.Json;

namespace SocialApp.ClientApi.Cli.Users;

public class UserCommander : Commander
{
    public UserCommander(CommanderState globalState) 
        : base("user", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CancellationToken cancel)
    {
        var regionName = GlobalState.GetCurrentRegion();

        if (command.Length == 1)
        {
            if (command[0] == "list")
            {
                return ListUsers(regionName);
            }
            else if(command.Length == 1)
            {
                var userName = command[0];

                var user = GlobalState.Get<User>("users", regionName, userName);
                if (user == null)
                {
                    user = CreateUser(userName, regionName);
                }

                GlobalState.SetCurrentUser(userName);
                Print(2, $"User {user.UserName} is selected.");

                return CommandResult.Success;
            }
        }
        else if (command.Length == 0)
        {
            return ShowCurrentUser();
        }
        else
        {
            return await base.ProcessAsync(command, cancel);
        }

        return CommandResult.Incomplete;
    }
    
    private User CreateUser(string userName, string regionName)
    {
        var id = Guid.NewGuid().ToString("N");
        var regions = GlobalState.GetMany<string>("regions");
        User selected = null;
        foreach (var region in regions)
        {
            var url = new Uri(region.Value.ToString(), UriKind.Absolute);
            var user = new User($"{userName}", id, $"{userName}@{id}.com", $"{userName}_{id}", region.Key)
            {
                Client = new SocialAppClient(url)
            };
            GlobalState.SaveUser(user, region.Key, userName);
            if(regionName == region.Key)
                selected = user;
        }
        
        Print(2, $"User created {selected.UserName} (@{selected.Handle})");
        return selected;
    }
    
    public Uri GetCurrentRegionUrl()
    {
        var regionName = GlobalState.Get<string>("currentRegion");
        if (string.IsNullOrWhiteSpace(regionName))
            throw new InvalidOperationException("There is no region selected. Run 'region set <region>'.");
        var url = GlobalState.Get<string>("regions", regionName);
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidDataContractException($"There is no region configured as '{regionName}'. Run 'regions add {regionName}' '<url>'.");
        return new Uri(url);
    }

    private CommandResult ShowCurrentUser()
    {
        var user = GlobalState.GetCurrentUserOrFail();
        Print(2, $"User {user.UserName} is selected. {JsonSerializer.Serialize(user)}");
        return CommandResult.Success;
    }

    private CommandResult ListUsers(string regionName)
    {
        var users = GlobalState.GetMany<User>("users", regionName);
        foreach (var user in users)
        {
            Print(3, $"- {user.Key}");
        }

        return CommandResult.Success;
    }
}