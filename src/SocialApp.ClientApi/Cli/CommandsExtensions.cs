using System.Runtime.Serialization;

namespace SocialApp.ClientApi.Cli;

public static class CommandsExtensions
{
    public static string GetCurrentRegion(this CommanderState state)
    {
        var regionName = state.Get<string>("currentRegion");
        if (string.IsNullOrWhiteSpace(regionName))
            throw new InvalidOperationException("There is no region selected. Run 'region set <region>'.");
        var region = state.Get<string>("regions", regionName);
        if (string.IsNullOrWhiteSpace(region))
            throw new InvalidDataContractException($"There is no region configured as '{regionName}'. Run 'regions add {regionName}' '<url>'.");
        return regionName;
    }

    public static void SaveUser(this CommanderState state, User user, string regionName, string userName)
    {
        state.Set(user, "users", regionName, userName);
    }
    
    public static void SetCurrentUser(this CommanderState state, string userName)
    {
        state.Set(userName, "currentUser");
    }
    
    public static User? TryGetCurrentUser(this CommanderState state)
    {
        var currentRegion = state.GetCurrentRegion();
        if (string.IsNullOrWhiteSpace(currentRegion))
            return null;
        var currentUser = state.Get<string>("currentUser");
        if (string.IsNullOrWhiteSpace(currentUser))
            return null;
        return state.Get<User>("users", currentRegion, currentUser);
    }
    
    public static User GetCurrentUserOrFail(this CommanderState state)
    {
        return state.TryGetCurrentUser() ?? throw new InvalidOperationException("There is no current user selected. Run 'user <user>'.");
    }
}