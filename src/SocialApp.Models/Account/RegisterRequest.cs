namespace SocialApp.Models.Account;

public class RegisterRequest
{
    public RegisterRequest()
    {
        
    }
    
    public RegisterRequest(string email, string handle, string displayName, string password)
    {
        Email = email;
        Handle = handle;
        DisplayName = displayName;
        Password = password;
    }

    public string Email { get; set; }
    public string Handle { get; set; }
    public string DisplayName { get; set; }
    public string Password { get; set; }
}