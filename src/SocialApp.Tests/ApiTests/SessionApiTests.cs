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
        TestUser generateUser(int i)
        {
            var name = $"user{i}_" + Guid.NewGuid().ToString("N");
            return new TestUser($"{name}@test.com", name, $"User{i}", name);
        }
        User1 = generateUser(1);
        User2 = generateUser(2);
        User3 = generateUser(3);
    }
    
    [Test, Order(1)]
    public async Task Should_Register_Accounts_And_Login()
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
            await client.Session.LoginAsync(new LoginRequest(u.Email, u.Password));
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
    public async Task Should_Follow_User1()
    {
        var client1 = Clients[User1];
        var client2 = Clients[User2];
        var client3 = Clients[User3];

        var user1Followings = await client1.Follow.GetFollowingsAsync();
        Assert.That(user1Followings, Is.Empty);

        await client1.Follow.AddAsync(User2.Handle);
        await client1.Follow.AddAsync(User3.Handle);
        
        var follows = await client1.Follow.GetFollowingsAsync();
        Assert.That(follows, Is.Not.Empty);
        Assert.That(follows, Is.EquivalentTo(new[] { User2.Handle, User3.Handle }));
        
        var followers = await client2.Follow.GetFollowersAsync();
        Assert.That(followers, Is.Not.Empty);
        Assert.That(followers, Is.EquivalentTo(new[] { User1.Handle }));
        
        followers = await client3.Follow.GetFollowersAsync();
        Assert.That(followers, Is.Not.Empty);
        Assert.That(followers, Is.EquivalentTo(new[] { User1.Handle }));
    }

    [Test, Order(3)]
    public async Task Should_Create_Content()
    {
        var client1 = Clients[User1];
        var client2 = Clients[User2];
        var client3 = Clients[User3];

        var user2ConversationId = await client2.Content.StartConversationAsync("User2 post");
        var posts = await client2.Content.GetConversationsAsync(User2.Handle);
        Assert.That(posts, Is.Not.Empty);
        Assert.That(posts.Count, Is.EqualTo(1));
        Assert.That(posts[0].ConversationId, Is.EqualTo(user2ConversationId));
        Assert.That(posts[0].Content, Is.EqualTo("User2 post"));

        await client1.Content.ReactToConversationAsync(User2.Handle, user2ConversationId, true);
        
        var user3CommentId = await client3.Content.CommentAsync(User2.Handle, user2ConversationId, "User3 comment");
        
        posts = await client2.Content.GetConversationsAsync(User2.Handle);
        Assert.That(posts, Is.Not.Empty);
        Assert.That(posts.Count, Is.EqualTo(1));
        Assert.That(posts[0].ConversationId, Is.EqualTo(user2ConversationId));
        Assert.That(posts[0].Content, Is.EqualTo("User2 post"));
        Assert.That(posts[0].CommentCount, Is.EqualTo(1));
        Assert.That(posts[0].LikeCount, Is.EqualTo(1));
        Assert.That(posts[0].ViewCount, Is.EqualTo(0));
        
        var post = await client3.Content.GetConversationAsync(User2.Handle, user2ConversationId);
        Assert.That(post.CommentCount, Is.EqualTo(1));
        Assert.That(post.ViewCount, Is.EqualTo(1));
        Assert.That(post.LikeCount, Is.EqualTo(1));
        Assert.That(post.LastComments.Count, Is.EqualTo(1));
        Assert.That(post.LastComments[0].CommentId, Is.EqualTo(user3CommentId));
        Assert.That(post.LastComments[0].Content, Is.EqualTo("User3 comment"));
        
        post = await client2.Content.GetConversationAsync(User2.Handle, user2ConversationId);
        Assert.That(post.CommentCount, Is.EqualTo(1));
        Assert.That(post.ViewCount, Is.EqualTo(2));
        Assert.That(post.LikeCount, Is.EqualTo(1));
        Assert.That(post.LastComments.Count, Is.EqualTo(1));
        Assert.That(post.LastComments[0].CommentId, Is.EqualTo(user3CommentId));
        Assert.That(post.LastComments[0].Content, Is.EqualTo("User3 comment"));
        
        await client3.Content.UpdateConversationAsync(User3.Handle, user3CommentId, "User3 comment UPDATED");
        post = await client3.Content.GetConversationAsync(User2.Handle, user2ConversationId);
        Assert.That(post.LastComments[0].Content, Is.EqualTo("User3 comment UPDATED"));
        
        await client1.Content.ReactToConversationAsync(User2.Handle, user2ConversationId, false);
        post = await client3.Content.GetConversationAsync(User2.Handle, user2ConversationId);
        Assert.That(post.LikeCount, Is.EqualTo(0));
        
        var ex = Assert.ThrowsAsync<HttpRequestException>(() => client3.Content.DeleteConversationAsync(User2.Handle, user2ConversationId));
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        await client2.Content.DeleteConversationAsync(User2.Handle, user2ConversationId);
        ex = Assert.ThrowsAsync<HttpRequestException>(() => client3.Content.GetConversationAsync(User2.Handle, user2ConversationId));
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        
        posts = await client2.Content.GetConversationsAsync(User2.Handle);
        Assert.That(posts, Is.Empty);
    }
}