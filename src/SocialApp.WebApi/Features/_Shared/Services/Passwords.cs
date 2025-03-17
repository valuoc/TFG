namespace SocialApp.WebApi.Features._Shared.Services;

public static class Passwords
{
    public static string HashPassword(string password)
    {
        return "$$" + password + "$$";
    }
}