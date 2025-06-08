using System.Text.Json;

namespace SocialApp.ClientApi.Cli.Users;

public class UserCommander : Commander
{
    public UserCommander(CommanderState globalState) 
        : base("user", globalState) { }
    
    public override async Task<CommandResult> ProcessAsync(string[] command, CommandContext context)
    {
        var regionName = GlobalState.GetCurrentRegion();

        if (command.Length >= 1)
        {
            if (command[0] == "list")
            {
                return ListUsers(regionName, context);
            }
            else if(command.Length >= 1)
            {
                var userName = command[0];

                var user = GlobalState.Get<User>("users", regionName, userName);
                if (user == null)
                {
                    if(command.Length > 1)
                    {
                        var email = command[1] + "@test_cli.com";
                        var handle = command[2];
                        user = CreateUser(userName, email, handle, regionName, context);
                    }
                    else
                    {
                        user = CreateUser(userName, regionName, context);
                    }
                }

                GlobalState.SetCurrentUser(userName);
                Print(2, $"User {user.UserName} is selected.", context);

                return CommandResult.Success;
            }
        }
        else if (command.Length == 0)
        {
            return ShowCurrentUser(context);
        }
        else
        {
            return await base.ProcessAsync(command, context);
        }

        return CommandResult.Incomplete;
    }

    private User? CreateUser(string userName, string email, string handle, string regionName, CommandContext context)
    {
        var regions = GlobalState.GetMany<string>("regions");
        User selected = null;
        foreach (var region in regions)
        {
            var url = new Uri(region.Value.ToString(), UriKind.Absolute);
            var user = new User(userName, Guid.NewGuid().ToString("N"), email, handle, region.Key)
            {
                Client = new SocialAppClient(url)
            };
            GlobalState.SaveUser(user, region.Key, userName);
            if(regionName == region.Key)
                selected = user;
        }
        
        Print(2, $"User created {selected.UserName} (@{selected.Handle})", context);
        return selected;
    }

    private User CreateUser(string userName, string regionName, CommandContext context)
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
        
        Print(2, $"User created {selected.UserName} (@{selected.Handle})", context);
        return selected;
    }

    private CommandResult ShowCurrentUser(CommandContext context)
    {
        var user = GlobalState.GetCurrentUserOrFail();
        Print(2, $"User {user.UserName} is selected. {JsonSerializer.Serialize(user)}", context);
        return CommandResult.Success;
    }

    private CommandResult ListUsers(string regionName, CommandContext context)
    {
        var users = GlobalState.GetMany<User>("users", regionName);
        foreach (var user in users)
        {
            Print(3, $"- {user.Key}: @{user.Value.Handle}", context);
        }

        return CommandResult.Success;
    }
}