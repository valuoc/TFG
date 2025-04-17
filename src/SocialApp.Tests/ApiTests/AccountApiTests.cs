using SocialApp.Models.Account;

namespace SocialApp.Tests.ApiTests;

public class AccountApiTests : ApiTestBase
{
    [Test]
    public async Task Should_Register_Account()
    {
        var user = Guid.NewGuid().ToString("N");
        var response = await Client.Account.RegisterAsync(new RegisterRequest
        {
            Email = $"{user}@test.com",
            Password = user,
            Handle = user,
            DisplayName = user
        });
        Assert.That(response, Is.Not.Null);
        Assert.That(response.UserId, Is.Not.Null);
    }
}