using System.Net;
using SocialApp.ClientApi;
using SocialApp.Models.Account;
using SocialApp.Models.Session;

namespace SocialApp.Tests.ApiTests;

public class SessionApiTests : ApiTestBase
{
    record TestUser(string Email, string Password, string DisplayName, string Handle);
    
    private TestUser User1 { get; set; }
    private TestUser User2 { get; set; }
    private TestUser User3 { get; set; }
    
    private Dictionary<TestUser, SocialAppClient> Clients = new Dictionary<TestUser, SocialAppClient>();
    
    [OneTimeSetUp]
    public async Task Setup()
    {
        User1 = GenerateUser();
        User2 = GenerateUser();
        User3 = GenerateUser();
    }

    private TestUser GenerateUser()
    {
        var id = Guid.NewGuid().ToString("N");
        return new TestUser($"{id}@test.com", id, id, id);
    }
    
    [Test, Order(1)]
    public async Task Should_Register_Account_And_Login()
    {
        async Task<SocialAppClient> registerAsync(TestUser u)
        {
            var client = CreateClient();
            await client.Account.RegisterAsync(new RegisterRequest
            {
                Email = u.Email,
                Password = u.Password,
                Handle = u.Handle,
                DisplayName = u.DisplayName
            });

            await client.Session.LoginAsync(new LoginRequest(u.Email, u.Password));
            await client.Session.LogoutAsync();
        
            var error = Assert.ThrowsAsync<HttpRequestException>(() => client.Session.LogoutAsync());
            Assert.That(error.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            return client;
        }
        
        TestUser[] users = [ User1, User2, User3 ];

        var clients = await Task.WhenAll([
            registerAsync(User1),
            registerAsync(User2),
            registerAsync(User3),
        ]);
        for (var i = 0; i < clients.Length; i++)
            Clients.Add(users[i], clients[i]);
    }
    
    [Test, Order(2)]
    public async Task Should_Follow()
    {
        await Clients[User1].Session.LoginAsync(new LoginRequest(User1.Email, User1.Password));
        await Clients[User1].Session.LogoutAsync();
        
        var error = Assert.ThrowsAsync<HttpRequestException>(() => Clients[User1].Session.LogoutAsync());
        Assert.That(error.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}