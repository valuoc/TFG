using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Exceptions;

namespace SocialApp.Tests.ServicesTests;

[Order(3)]
public class ContentServiceTests: ServiceTestsBase
{
    [Test, Order(1)]
    public async Task Content_Post_CanBe_Added()
    {
        var user1 = await CreateUserAsync();

        var post1Id = await ContentService.CreatePostAsync(user1, "This is a post.", OperationContext.None());

        var post1 = await ContentService.GetPostAsync(user1, post1Id, 5, OperationContext.None());

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
        
        var commentId = await ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "This is a comment.", OperationContext.None());
        
        // Comment is a post on its own
        var post2 = await ContentService.GetPostAsync(user2, commentId, 5, OperationContext.None());
        Assert.That(post2, Is.Not.Null);
        Assert.That(post2.ViewCount, Is.EqualTo(1));
        Assert.That(post2.CommentCount, Is.EqualTo(0));
        Assert.That(post2.Content, Is.EqualTo("This is a comment."));
        
        // Comment appears as comment
        var post1 = await ContentService.GetPostAsync(user1, post1Id, 5, OperationContext.None());
        Assert.That(post1, Is.Not.Null);
        Assert.That(post1.CommentCount, Is.EqualTo(1));
        Assert.That(post1.Content, Is.EqualTo("This is a post."));
        
        commentId = await ContentService.CreateCommentAsync(user1, user1.UserId, post1Id, "This is a self-reply.", OperationContext.None());
        
        post1 = await ContentService.GetPostAsync(user1, commentId, 5, OperationContext.None());
        Assert.That(post1, Is.Not.Null);
        Assert.That(post1.ViewCount, Is.EqualTo(1));
        Assert.That(post1.CommentCount, Is.EqualTo(0));
        Assert.That(post1.Content, Is.EqualTo("This is a self-reply."));
        
        post1 = await ContentService.GetPostAsync(user1, post1Id, 5, OperationContext.None());
        Assert.That(post1, Is.Not.Null);
        Assert.That(post1.ViewCount, Is.EqualTo(2));
        Assert.That(post1.CommentCount, Is.EqualTo(2));
        Assert.That(post1.Content, Is.EqualTo("This is a post."));
        Assert.That(post1.LastComments[^1].Content, Is.EqualTo("This is a self-reply."));
    }

    [Test, Order(3)]
    public async Task Content_Comment_Is_SortedAndPaginated()
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
            await ContentService.CreateCommentAsync(moment%2==0?user2:user3, user1.UserId, post1Id, moment.ToString(), context);
        }

        var post = await ContentService.GetPostAsync(user1, post1Id, 5, OperationContext.None());
        Assert.That(post, Is.Not.Null);
        Assert.That(post.LastComments.Count, Is.EqualTo(5));
        Assert.That(post.CommentCount, Is.EqualTo(10));
        for (var i = 0; i < 5; i++)
            Assert.That(post.LastComments[i].Content, Is.EqualTo((i+6).ToString()));
        
        var prevComments = await ContentService.GetPreviousCommentsAsync(user1.UserId, post1Id, post.LastComments[0].PostId, 2, OperationContext.None());
        Assert.That(prevComments, Is.Not.Null);
        Assert.That(prevComments.Count, Is.EqualTo(2));
        for (var i = 0; i < 2; i++)
            Assert.That(prevComments[i].Content, Is.EqualTo((i+4).ToString()));
        
        prevComments = await ContentService.GetPreviousCommentsAsync(user1.UserId, post1Id, prevComments[0].PostId, 3, OperationContext.None());
        Assert.That(prevComments, Is.Not.Null);
        Assert.That(prevComments.Count, Is.EqualTo(3));
        for (var i = 0; i < 3; i++)
            Assert.That(prevComments[i].Content, Is.EqualTo((i+1).ToString()));
        
        prevComments = await ContentService.GetPreviousCommentsAsync(user1.UserId, post1Id, prevComments[0].PostId, 3, OperationContext.None());
        Assert.That(prevComments, Is.Not.Null);
        Assert.That(prevComments, Is.Empty);
    }
    
    [Test, Order(4)]
    public async Task Content_Comment_Counts_Populate()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var post1Id = await ContentService.CreatePostAsync(user1, "Root", OperationContext.None());
        var post2Id = await ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "Child", OperationContext.None());
        var post3Id = await ContentService.CreateCommentAsync(user1, user2.UserId, post2Id, "Grandchild", OperationContext.None());
        
        var post1 = await ContentService.GetPostAsync(user1, post1Id, 5, OperationContext.None());
        Assert.That(post1.CommentCount, Is.EqualTo(1));
        Assert.That(post1.LastComments[0].CommentCount, Is.EqualTo(1));
        
        var post2 = await ContentService.GetPostAsync(user2, post2Id, 5, OperationContext.None());
        Assert.That(post2.CommentCount, Is.EqualTo(1));
        Assert.That(post2.LastComments[0].CommentCount, Is.EqualTo(0));
    }
    
    [Test, Order(5)]
    public async Task Content_Paginate_Posts()
    {
        var now = DateTimeOffset.UtcNow;
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();
        
        var moments = Enumerable
            .Range(0, 10)
            .Select(i => i + 1)
            .OrderBy(x => Guid.NewGuid())
            .ToArray();

        for (var i = 0; i < moments.Length; i++)
        {
            var moment = moments[i];
            var context = new OperationContext(CancellationToken.None);
            context.SetTime(now.AddSeconds(moment));
            var post1Id = await ContentService.CreatePostAsync(user1, moment.ToString(), context);

            if (i % 3 == 0)
            {
                var post12d = await ContentService.CreatePostAsync(user2, moment.ToString(), context);
                await ContentService.CreateCommentAsync(user1, user2.UserId, post12d, moment + "reply!!", context);
            }
        }

        var posts = await ContentService.GetUserPostsAsync(user1.UserId, null, 5, OperationContext.None());
        Assert.That(posts, Is.Not.Null);
        Assert.That(posts.Count, Is.EqualTo(5));
        for (var i = 0; i < 5; i++)
            Assert.That(posts[i].Content, Is.EqualTo((10 - i).ToString()));
        
        posts = await ContentService.GetUserPostsAsync(user1.UserId, posts[^1].PostId, 5, OperationContext.None());
        Assert.That(posts, Is.Not.Null);
        Assert.That(posts.Count, Is.EqualTo(5));
        for (var i = 0; i < 5; i++)
            Assert.That(posts[i].Content, Is.EqualTo((5 - i).ToString()));
        
        posts = await ContentService.GetUserPostsAsync(user1.UserId, posts[^1].PostId, 5, OperationContext.None());
        Assert.That(posts, Is.Not.Null);
        Assert.That(posts.Count, Is.EqualTo(0));
    }
    
    [Test, Order(6)]
    public async Task Content_Comment_RecoverFrom_Failure()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var post1Id = await ContentService.CreatePostAsync(user1, "Root", OperationContext.None());
        var context = OperationContext.None();
        context.FailOnSignal("create-comment-post", CreateCosmoException());
        Assert.ThrowsAsync<ContentException>( () => ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "Child", context).AsTask());
        
        var post  = await ContentService.GetPostAsync(user1, post1Id, 5, OperationContext.None());
        var commentPost = await ContentService.GetPostAsync(user2, post.LastComments[0].PostId, 5, OperationContext.None());
        Assert.That(commentPost, Is.Not.Null);
        Assert.That(commentPost.Content, Is.EqualTo("Child"));
    }
    
    [Test, Order(7)]
    public async Task Content_Can_Update()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var post1Id = await ContentService.CreatePostAsync(user1, "Root", OperationContext.None());
        var post2Id = await ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "Child", OperationContext.None());
        await ContentService.UpdatePostAsync(user2, post2Id, "Updated!", OperationContext.None());
        
        var post1 = await ContentService.GetPostAsync(user1, post1Id, 5, OperationContext.None());
        Assert.That(post1.LastComments[0].Content, Is.EqualTo("Updated!"));
        
        var post2 = await ContentService.GetPostAsync(user2, post2Id, 5, OperationContext.None());
        Assert.That(post2.Content, Is.EqualTo("Updated!"));
    }
}