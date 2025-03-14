using SocialApp.WebApi.Features.Services;

namespace SocialApp.Tests.ServicesTests;

[Order(3)]
public class ContentServiceTests: ServiceTestsBase
{
    [Test, Order(1)]
    public async Task Content_Post_CanBe_Added()
    {
        var user1 = await CreateUserAsync();

        var postId = await ContentService.CreatePostAsync(user1, "holahola", OperationContext.None());

        var posts = await ContentService.GetPostAsync(user1.UserId, postId, OperationContext.None());

        Assert.That(posts, Is.Not.Null);
        Assert.That(posts.ViewCount, Is.EqualTo(1));

    }
}