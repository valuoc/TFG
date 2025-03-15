using SocialApp.WebApi.Features.Services;

namespace SocialApp.Tests.ServicesTests;

[Order(3)]
public class ContentServiceTests: ServiceTestsBase
{
    [Test, Order(1)]
    public async Task Content_Post_CanBe_Added()
    {
        var user1 = await CreateUserAsync();

        var post1Id = await ContentService.CreatePostAsync(user1, "This is a post.", OperationContext.None());

        var post1 = await ContentService.GetPostAsync(user1.UserId, post1Id, OperationContext.None());

        Assert.That(post1, Is.Not.Null);
        Assert.That(post1.ViewCount, Is.EqualTo(1));
        Assert.That(post1.CommentCount, Is.EqualTo(0));
        Assert.That(post1.Content, Is.EqualTo("This is a post."));
    }
    
    [Test, Order(2)]
    public async Task Content_Comment_CanBe_Added()
    {
        var user1 = await CreateUserAsync();

        var post1Id = await ContentService.CreatePostAsync(user1, "This is a post.", OperationContext.None());
        
        var user2 = await CreateUserAsync();
        
        var commentId = await ContentService.CommentAsync(user2, user1.UserId, post1Id, "This is a comment.", OperationContext.None());
        
        // Comment is a post on its own
        var post2 = await ContentService.GetPostAsync(user2.UserId, commentId, OperationContext.None());
        Assert.That(post2, Is.Not.Null);
        Assert.That(post2.ViewCount, Is.EqualTo(1));
        Assert.That(post2.CommentCount, Is.EqualTo(0));
        Assert.That(post2.Content, Is.EqualTo("This is a comment."));
        
        // Comment appears as comment
        var post1 = await ContentService.GetPostAsync(user1.UserId, post1Id, OperationContext.None());
        Assert.That(post1, Is.Not.Null);
        Assert.That(post1.CommentCount, Is.EqualTo(1));
        Assert.That(post1.Content, Is.EqualTo("This is a post."));
        
        commentId = await ContentService.CommentAsync(user1, user1.UserId, post1Id, "This is a self-reply.", OperationContext.None());
        
        post1 = await ContentService.GetPostAsync(user1.UserId, commentId, OperationContext.None());
        Assert.That(post1, Is.Not.Null);
        Assert.That(post1.ViewCount, Is.EqualTo(1));
        Assert.That(post1.CommentCount, Is.EqualTo(0));
        Assert.That(post1.Content, Is.EqualTo("This is a self-reply."));
        
        post1 = await ContentService.GetPostAsync(user1.UserId, post1Id, OperationContext.None());
        Assert.That(post1, Is.Not.Null);
        Assert.That(post1.ViewCount, Is.EqualTo(2));
        Assert.That(post1.CommentCount, Is.EqualTo(2));
        Assert.That(post1.Content, Is.EqualTo("This is a post."));
        Assert.That(post1.LastComments[^1].Content, Is.EqualTo("This is a self-reply."));
    }

    [Test, Order(3)]
    public async Task Content_Comment_Is_Sorted()
    {
        var now = DateTimeOffset.UtcNow;
        
        var user1 = await CreateUserAsync();

        var context = new OperationContext(CancellationToken.None);
        context.SetTime(now);
        var post1Id = await ContentService.CreatePostAsync(user1, "This is a post.", context);

        var user2 = await CreateUserAsync();
        var user3 = await CreateUserAsync();

        var moments = Enumerable
            .Range(0, 10)
            .Select(i => i + 1)
            .OrderBy(x => Guid.NewGuid())
            .ToArray();

        for (var i = 0; i < moments.Length; i++)
        {
            var moment = moments[i];
            context = new OperationContext(CancellationToken.None);
            context.SetTime(now.AddSeconds(moment));
            await ContentService.CommentAsync(moment%2==0?user2:user3, user1.UserId, post1Id, moment.ToString(), context);
        }

        var post = await ContentService.GetPostAsync(user1.UserId, post1Id, OperationContext.None());
        Assert.That(post, Is.Not.Null);
        for (var i = 1; i <= 10; i++)
        {
            Assert.That(post.LastComments[i-1].Content, Is.EqualTo(i.ToString()));
        }
        
        /*
         where c.pk = "user:53041ffd44414ba1b5ad970b832b4afd" and c.id >= "post:01JPCKYQZCEVS14QCWGC810K7Q" and c.id < "post:01JPCKYQZCEVS14QCWGC810K7Q:z" order by c.id desc
         */
    }
}