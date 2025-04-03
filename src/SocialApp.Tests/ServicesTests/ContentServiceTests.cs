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

        var context = OperationContext.New();
        var post1Id = await ContentService.CreateThreadAsync(user1, "This is a post.", context);
        Console.WriteLine(context.OperationCharge);

        context = OperationContext.New();
        var post1 = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, context);
        Console.WriteLine(context.OperationCharge);

        Assert.That(post1, Is.Not.Null);
        Assert.That(post1.ViewCount, Is.EqualTo(1));
        Assert.That(post1.CommentCount, Is.EqualTo(0));
        Assert.That(post1.Content, Is.EqualTo("This is a post."));
    }
    
    [Test, Order(2)]
    public async Task Content_Comment_CanBe_Added()
    {
        var user1 = await CreateUserAsync();

        var post1Id = await ContentService.CreateThreadAsync(user1, "This is a post.", OperationContext.New());
        
        var user2 = await CreateUserAsync();
        
        var commentId = await ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "This is a comment.", OperationContext.New());
        
        // Comment is a post on its own
        var post2 = await ContentService.GetThreadAsync(user2, user2.UserId, commentId, 5, OperationContext.New());
        Assert.That(post2, Is.Not.Null);
        Assert.That(post2.ViewCount, Is.EqualTo(1));
        Assert.That(post2.CommentCount, Is.EqualTo(0));
        Assert.That(post2.Content, Is.EqualTo("This is a comment."));
        
        // Comment appears as comment
        var post1 = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, OperationContext.New());
        Assert.That(post1, Is.Not.Null);
        Assert.That(post1.CommentCount, Is.EqualTo(1));
        Assert.That(post1.Content, Is.EqualTo("This is a post."));
        
        commentId = await ContentService.CreateCommentAsync(user1, user1.UserId, post1Id, "This is a self-reply.", OperationContext.New());
        
        post1 = await ContentService.GetThreadAsync(user1, user1.UserId, commentId, 5, OperationContext.New());
        Assert.That(post1, Is.Not.Null);
        Assert.That(post1.ViewCount, Is.EqualTo(1));
        Assert.That(post1.CommentCount, Is.EqualTo(0));
        Assert.That(post1.Content, Is.EqualTo("This is a self-reply."));
        
        post1 = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, OperationContext.New());
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
        var post1Id = await ContentService.CreateThreadAsync(user1, "This is a post.", context);

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

        context = OperationContext.New();
        var post = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, context);
        Console.WriteLine(context.OperationCharge);
        Assert.That(post, Is.Not.Null);
        Assert.That(post.LastComments.Count, Is.EqualTo(5));
        Assert.That(post.CommentCount, Is.EqualTo(10));
        for (var i = 0; i < 5; i++)
            Assert.That(post.LastComments[i].Content, Is.EqualTo((i+6).ToString()));
        
        var prevComments = await ContentService.GetPreviousCommentsAsync(user1, user1.UserId, post1Id, post.LastComments[0].CommentId, 2, OperationContext.New());
        Assert.That(prevComments, Is.Not.Null);
        Assert.That(prevComments.Count, Is.EqualTo(2));
        for (var i = 0; i < 2; i++)
            Assert.That(prevComments[i].Content, Is.EqualTo((i+4).ToString()));
        
        prevComments = await ContentService.GetPreviousCommentsAsync(user1, user1.UserId, post1Id, prevComments[0].CommentId, 3, OperationContext.New());
        Assert.That(prevComments, Is.Not.Null);
        Assert.That(prevComments.Count, Is.EqualTo(3));
        for (var i = 0; i < 3; i++)
            Assert.That(prevComments[i].Content, Is.EqualTo((i+1).ToString()));
        
        prevComments = await ContentService.GetPreviousCommentsAsync(user1, user1.UserId, post1Id, prevComments[0].CommentId, 3, OperationContext.New());
        Assert.That(prevComments, Is.Not.Null);
        Assert.That(prevComments, Is.Empty);
    }
    
    [Test, Order(4)]
    public async Task Content_Comment_Counts_Populate()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var post1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var post2Id = await ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "Child", OperationContext.New());
        var post3Id = await ContentService.CreateCommentAsync(user1, user2.UserId, post2Id, "Grandchild", OperationContext.New());

        await Task.Delay(2_000);
        
        var post1 = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, OperationContext.New());
        Assert.That(post1.CommentCount, Is.EqualTo(1));
        Assert.That(post1.LastComments[0].CommentCount, Is.EqualTo(1));
        
        var post2 = await ContentService.GetThreadAsync(user2, user2.UserId, post2Id, 5, OperationContext.New());
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
        
        OperationContext context;
        for (var i = 0; i < moments.Length; i++)
        {
            var moment = moments[i];
            context = new OperationContext(CancellationToken.None);
            context.SetTime(now.AddSeconds(moment));
            var post1Id = await ContentService.CreateThreadAsync(user1, moment.ToString(), context);

            if (i % 3 == 0)
            {
                var post12d = await ContentService.CreateThreadAsync(user2, moment.ToString(), context);
                await ContentService.CreateCommentAsync(user1, user2.UserId, post12d, moment + "reply!!", context);
            }
        }

        context = OperationContext.New();
        var posts = await ContentService.GetUserPostsAsync(user1, null, 5, context);
        Console.WriteLine(context.OperationCharge);
        Assert.That(posts, Is.Not.Null);
        Assert.That(posts.Count, Is.EqualTo(5));
        for (var i = 0; i < 5; i++)
            Assert.That(posts[i].Content, Is.EqualTo((10 - i).ToString()));
        
        posts = await ContentService.GetUserPostsAsync(user1, posts[^1].ThreadId, 5, OperationContext.New());
        Assert.That(posts, Is.Not.Null);
        Assert.That(posts.Count, Is.EqualTo(5));
        for (var i = 0; i < 5; i++)
            Assert.That(posts[i].Content, Is.EqualTo((5 - i).ToString()));
        
        posts = await ContentService.GetUserPostsAsync(user1, posts[^1].ThreadId, 5, OperationContext.New());
        Assert.That(posts, Is.Not.Null);
        Assert.That(posts.Count, Is.EqualTo(0));
    }
    
    [Test, Order(6)]
    public async Task Content_Comment_RecoverFrom_PostPartialFailure()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var post1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var context = OperationContext.New();
        context.FailOnSignal("create-comment-post", CreateCosmoException());
        await ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "Child", context);

        await Task.Delay(1_000);
        
        var post  = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, OperationContext.New());
        var commentPost = await ContentService.GetThreadAsync(user2, user2.UserId, post.LastComments[0].CommentId, 5, OperationContext.New());
        Assert.That(commentPost, Is.Not.Null);
        Assert.That(commentPost.Content, Is.EqualTo("Child"));
    }
    
    [Test, Order(7)]
    public async Task Content_Can_Update()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var post1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var post2Id = await ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "Child", OperationContext.New());
        await ContentService.UpdateThreadAsync(user2, post2Id, "Updated!", OperationContext.New());
        
        var post1 = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, OperationContext.New());
        Assert.That(post1.LastComments[0].Content, Is.EqualTo("Updated!"));
        
        var post2 = await ContentService.GetThreadAsync(user2, user2.UserId, post2Id, 5, OperationContext.New());
        Assert.That(post2.Content, Is.EqualTo("Updated!"));
    }
    
    [Test, Order(8)]
    public async Task Content_Comment_RecoverFrom_CommentPartialFailure()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var post1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var commentId = await ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "Child", OperationContext.New());
        
        var context = OperationContext.New();
        context.FailOnSignal("update-comment", CreateCosmoException());
        await ContentService.UpdateThreadAsync(user2, commentId, "Child !!!", context).AsTask();

        await Task.Delay(2_000);
        
        var commentPost = await ContentService.GetThreadAsync(user2, user2.UserId, commentId, 5, OperationContext.New());
        Assert.That(commentPost, Is.Not.Null);
        Assert.That(commentPost.Content, Is.EqualTo("Child !!!"));
        
        var post  = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, OperationContext.New());
        Assert.That(post.CommentCount, Is.EqualTo(1));
        Assert.That(post.LastComments[0].Content, Is.EqualTo("Child !!!"));
    }

    [Test, Order(9)]
    public async Task Content_Can_Delete()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var post1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var comment2Id = await ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "Child", OperationContext.New());
        var comment1Id = await ContentService.CreateCommentAsync(user1, user1.UserId, post1Id, "Child Reply !!!", OperationContext.New());

        var post = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, OperationContext.New());
        Assert.That(post, Is.Not.Null);
        Assert.That(post.CommentCount, Is.EqualTo(2));
        Assert.That(post.LastComments.Count, Is.EqualTo(2));
        Assert.That(post.LastComments[^1].Content, Is.EqualTo("Child Reply !!!"));

        var context = OperationContext.New();
        await ContentService.DeleteThreadAsync(user2, comment2Id, context);
        Console.WriteLine(context.OperationCharge);
        Console.WriteLine(context.DebugMetrics);
        
        Assert.ThrowsAsync<ContentException>(() => ContentService.GetThreadAsync(user2, user2.UserId, comment2Id, 5, OperationContext.New()).AsTask());
        
        post = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, OperationContext.New());
        Assert.That(post, Is.Not.Null);
        Assert.That(post.CommentCount, Is.EqualTo(1));
        Assert.That(post.LastComments.Count, Is.EqualTo(1));
        Assert.That(post.LastComments[^1].Content, Is.EqualTo("Child Reply !!!"));
    }
    
    [Test, Order(10)]
    public async Task Content_Can_RecoverDelete()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var post1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var comment2Id = await ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "Child", OperationContext.New());
        var comment1Id = await ContentService.CreateCommentAsync(user1, user1.UserId, post1Id, "Child Reply !!!", OperationContext.New());

        Console.WriteLine("delete " + comment2Id);
        var context = OperationContext.New();
        context.FailOnSignal("delete-comment", CreateCosmoException());
        await ContentService.DeleteThreadAsync(user2, comment2Id, context);
        
        Assert.ThrowsAsync<ContentException>(() => ContentService.GetThreadAsync(user2, user2.UserId, comment2Id, 5, OperationContext.New()).AsTask());

        await Task.Delay(5_000);
        
        var post = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, OperationContext.New());
        Assert.That(post, Is.Not.Null);
        Assert.That(post.CommentCount, Is.EqualTo(1));
        Assert.That(post.LastComments.Count, Is.EqualTo(1));
        Assert.That(post.LastComments[^1].Content, Is.EqualTo("Child Reply !!!"));
    }
    
    [Order(11)]
    //[TestCase(10)]
    public async Task Content_Costs(int size)
    {
        // 1 RU = 1 point read of 1kb
        // https://cosmos.azure.com/capacitycalculator/
        
        // Point reads: key/value lookups on the item ID and partition key
        // https://learn.microsoft.com/en-us/azure/cosmos-db/optimize-cost-reads-writes#point-reads
        
        // https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query-metrics-performance
        // https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/index-metrics?tabs=dotnet
        // https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/query-metrics
        
        var now = DateTimeOffset.UtcNow;
        var writeRcu = 0d;
        
        var user1 = await CreateUserAsync();

        var context = new OperationContext(CancellationToken.None);
        context.SetTime(now);
        var post1Id = await ContentService.CreateThreadAsync(user1, "X".PadLeft(size, 'X'), context);
        Console.WriteLine(context.OperationCharge);
        writeRcu += context.OperationCharge;
        
        context = new OperationContext(CancellationToken.None);
        context.SetTime(now);
        await ContentService.UpdateThreadAsync(user1, post1Id, "Y".PadLeft(size, 'Y'), context);
        Console.WriteLine(context.OperationCharge);
        writeRcu += context.OperationCharge;

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
            context = OperationContext.New();
            context.SetTime(now.AddSeconds(moment));
            await ContentService.CreateCommentAsync(moment%2==0?user2:user3, user1.UserId, post1Id, "X".PadLeft(size, 'X'), context);
            Console.WriteLine(context.OperationCharge);
            writeRcu += context.OperationCharge;
        }

        context = OperationContext.New();
        await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, context);
        Console.WriteLine(context.OperationCharge);
        Console.WriteLine(writeRcu);
    }
}