namespace SocialApp.ClientApi.Cli;

public record User(string UserName, string Password, string Email, string Handle, string RegionName)
{
    public SocialAppClient Client { get; init; }
}