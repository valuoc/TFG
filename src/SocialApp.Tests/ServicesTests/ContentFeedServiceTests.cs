using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Exceptions;

namespace SocialApp.Tests.ServicesTests;

[Order(1)]
public class ContentFeedServiceTests: ServiceTestsBase
{
    [Test, Order(0)]
    public async Task Content_Comment_Counts_Populate()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var post1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var post2Id = await ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "Child", OperationContext.New());
        var post3Id = await ContentService.CreateCommentAsync(user1, user2.UserId, post2Id, "Grandchild", OperationContext.New());

        // Uses Change Feed
        await Task.Delay(2_000);
        
        var post1 = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, OperationContext.New());
        Assert.That(post1.CommentCount, Is.EqualTo(1));
        Assert.That(post1.LastComments[0].CommentCount, Is.EqualTo(1));
        
        var post2 = await ContentService.GetThreadAsync(user2, user2.UserId, post2Id, 5, OperationContext.New());
        Assert.That(post2.CommentCount, Is.EqualTo(1));
        Assert.That(post2.LastComments[0].CommentCount, Is.EqualTo(0));
    }
    
    [Test, Order(1)]
    public async Task Content_Comment_RecoverFrom_PostPartialFailure()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var post1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var context = OperationContext.New();
        context.FailOnSignal("create-comment-post", CreateCosmoException());
        await ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "Child", context);

        await Task.Delay(2_000);
        
        var post  = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, OperationContext.New());
        var commentPost = await ContentService.GetThreadAsync(user2, user2.UserId, post.LastComments[0].CommentId, 5, OperationContext.New());
        Assert.That(commentPost, Is.Not.Null);
        Assert.That(commentPost.Content, Is.EqualTo("Child"));
    }
    
    [Test, Order(2)]
    public async Task Content_Comment_RecoverFrom_CommentPartialFailure()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var post1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var commentId = await ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "Child", OperationContext.New());
        
        var context = OperationContext.New();
        context.FailOnSignal("update-comment", CreateCosmoException());
        await ContentService.UpdateThreadAsync(user2, commentId, "Child !!!", context);

        await Task.Delay(2_000);
        
        var commentPost = await ContentService.GetThreadAsync(user2, user2.UserId, commentId, 5, OperationContext.New());
        Assert.That(commentPost, Is.Not.Null);
        Assert.That(commentPost.Content, Is.EqualTo("Child !!!"));
        
        var post  = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, OperationContext.New());
        Assert.That(post.CommentCount, Is.EqualTo(1));
        Assert.That(post.LastComments[0].Content, Is.EqualTo("Child !!!"));
    }
    
    [Test, Order(3)]
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
        
        Assert.ThrowsAsync<ContentException>(() => ContentService.GetThreadAsync(user2, user2.UserId, comment2Id, 5, OperationContext.New()));

        await Task.Delay(5_000);
        
        var post = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, OperationContext.New());
        Assert.That(post, Is.Not.Null);
        Assert.That(post.CommentCount, Is.EqualTo(1));
        Assert.That(post.LastComments.Count, Is.EqualTo(1));
        Assert.That(post.LastComments[^1].Content, Is.EqualTo("Child Reply !!!"));
    }
    

    [Order(4)]
    [TestCase(true)]
    [TestCase(false)]
    public async Task Content_Like_Recovers(bool like)
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var post1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var post2Id = await ContentService.CreateCommentAsync(user2, user1.UserId, post1Id, "Child", OperationContext.New());

        var context = OperationContext.New();
        context.FailOnSignal("react-thread", CreateCosmoException());
        await ContentService.ReactToThreadAsync(user1, user2.UserId, post2Id, like, context);

        await Task.Delay(2_000);
        
        var post2 = await ContentService.GetThreadAsync(user2, user2.UserId, post2Id, 5, OperationContext.New());
        Assert.That(post2.LikeCount, Is.EqualTo(like ? 1 : 0));

        var post1 = await ContentService.GetThreadAsync(user1, user1.UserId, post1Id, 5, OperationContext.New());
        Assert.That(post1.LastComments[0].LikeCount, Is.EqualTo(like ? 1 : 0));
    }
}