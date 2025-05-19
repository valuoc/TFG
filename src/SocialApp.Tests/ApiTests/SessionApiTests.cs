using System.Net;
using SocialApp.ClientApi;
using SocialApp.Models.Account;
using SocialApp.Models.Content;
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
            var client = CreateClient("http://localhost:7000");
            var health = await client.HealthAsync();
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
        Assert.That(post.Root.CommentCount, Is.EqualTo(1));
        Assert.That(post.Root.ViewCount, Is.EqualTo(1));
        Assert.That(post.Root.LikeCount, Is.EqualTo(1));
        Assert.That(post.LastComments.Count, Is.EqualTo(1));
        Assert.That(post.LastComments[0].CommentId, Is.EqualTo(user3CommentId));
        Assert.That(post.LastComments[0].Content, Is.EqualTo("User3 comment"));
        
        post = await client2.Content.GetConversationAsync(User2.Handle, user2ConversationId);
        Assert.That(post.Root.CommentCount, Is.EqualTo(1));
        Assert.That(post.Root.ViewCount, Is.EqualTo(2));
        Assert.That(post.Root.LikeCount, Is.EqualTo(1));
        Assert.That(post.LastComments.Count, Is.EqualTo(1));
        Assert.That(post.LastComments[0].CommentId, Is.EqualTo(user3CommentId));
        Assert.That(post.LastComments[0].Content, Is.EqualTo("User3 comment"));
        
        await client3.Content.UpdateConversationAsync(User3.Handle, user3CommentId, "User3 comment UPDATED");
        post = await client3.Content.GetConversationAsync(User2.Handle, user2ConversationId);
        Assert.That(post.LastComments[0].Content, Is.EqualTo("User3 comment UPDATED"));
        
        await client1.Content.ReactToConversationAsync(User2.Handle, user2ConversationId, false);
        post = await client3.Content.GetConversationAsync(User2.Handle, user2ConversationId);
        Assert.That(post.Root.LikeCount, Is.EqualTo(0));
        
        var ex = Assert.ThrowsAsync<HttpRequestException>(() => client3.Content.DeleteConversationAsync(User2.Handle, user2ConversationId));
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        await client2.Content.DeleteConversationAsync(User2.Handle, user2ConversationId);
        ex = Assert.ThrowsAsync<HttpRequestException>(() => client3.Content.GetConversationAsync(User2.Handle, user2ConversationId));
        Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        
        posts = await client2.Content.GetConversationsAsync(User2.Handle);
        Assert.That(posts, Is.Empty);
    }

    [Test, Order(4)]
    public async Task Should_Paginate_Content()
    {
        var client1 = Clients[User1];
        var client2 = Clients[User2];
        var client3 = Clients[User3];

        string lastUser2ConversationId = string.Empty;
        string lastUser3ConversationId = string.Empty;
        
        for (var i = 1; i <= 20; i++)
        {
            lastUser2ConversationId = await client2.Content.StartConversationAsync("User2 post " + i);
            lastUser3ConversationId = await client3.Content.StartConversationAsync("User3 post " + i);
        }

        for (var i = 1; i <= 10; i++)
        {
            await client1.Content.CommentAsync(User3.Handle, lastUser3ConversationId,  "User1 comment " + i);
        }
        
        var posts = await client2.Content.GetConversationsAsync(User2.Handle);
        Assert.That(posts, Is.Not.Empty);
        Assert.That(posts.Count, Is.EqualTo(10));
        for (var i = 0; i < 10; i++)
            Assert.That(posts[i].Content, Is.EqualTo("User2 post " + (20 - i)));
        
        posts = await client2.Content.GetConversationsAsync(User2.Handle, posts.Last().ConversationId);
        Assert.That(posts, Is.Not.Empty);
        Assert.That(posts.Count, Is.EqualTo(10));
        for (var i = 0; i < 10; i++)
            Assert.That(posts[i].Content, Is.EqualTo("User2 post " + (10 - i)));
        
        posts = await client2.Content.GetConversationsAsync(User2.Handle, posts.Last().ConversationId);
        Assert.That(posts, Is.Empty);
        
        IReadOnlyList<ConversationComment> comments = (await client3.Content.GetConversationAsync(User3.Handle, lastUser3ConversationId)).LastComments;
        Assert.That(comments, Is.Not.Empty);
        Assert.That(comments.Count, Is.EqualTo(5));
        for (var i = 0; i < 5; i++)
            Assert.That(comments[i].Content, Is.EqualTo("User1 comment " + (6 + i)));
        
        comments = await client3.Content.GetConversationCommentsBeforeAsync(User3.Handle, lastUser3ConversationId, comments.First().CommentId);
        Assert.That(comments, Is.Not.Empty);
        Assert.That(comments.Count, Is.EqualTo(5));
        for (var i = 0; i < 5; i++)
            Assert.That(comments[i].Content, Is.EqualTo("User1 comment " + (1 + i)));
        
        comments = await client3.Content.GetConversationCommentsBeforeAsync(User3.Handle, lastUser3ConversationId, comments.First().CommentId);
        Assert.That(comments, Is.Empty);
    }

    [Test, Order(5)]
    public async Task Should_Paginate_Feed()
    {
        var client1 = Clients[User1];

        var posts1 = await client1.Feed.FeedAsync();
        Assert.That(posts1, Is.Not.Empty);
        Assert.That(posts1.Count, Is.EqualTo(10));
        Assert.That(posts1[^2].Content, Is.EqualTo("User3 post 16"));
        Assert.That(posts1[^1].Content, Is.EqualTo("User2 post 16"));
        
        var posts2 = await client1.Feed.FeedAsync(posts1.Last().ConversationId);
        Assert.That(posts2, Is.Not.Empty);
        Assert.That(posts2.Count, Is.EqualTo(10));
        Assert.That(posts2[0].Content, Is.EqualTo("User3 post 15"));
        Assert.That(posts2[1].Content, Is.EqualTo("User2 post 15"));
    }
}