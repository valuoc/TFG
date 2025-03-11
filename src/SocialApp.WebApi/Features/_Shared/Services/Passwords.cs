namespace SocialApp.WebApi.Features.Services;

public static class Passwords
{
    public static string HashPassword(string password)
    {
        return "$$" + password + "$$";
    }
}