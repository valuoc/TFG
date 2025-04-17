using SocialApp.Models.Account;
using SocialApp.Models.Session;

namespace SocialApp.Tests.ApiTests;

public class SessionApiTests : ApiTestBase
{
    [Test]
    public async Task Should_Register_Account_And_Login()
    {
        var user = Guid.NewGuid().ToString("N");
        var email = $"{user}@test.com";
        await Client.Account.RegisterAsync(new RegisterRequest
        {
            Email = email,
            Password = user,
            Handle = user,
            DisplayName = user
        });

        await Client.Session.LoginAsync(new LoginRequest(email, user));
        await Client.Session.LogoutAsync();
    }
}