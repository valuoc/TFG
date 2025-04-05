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

        var thread1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var thread2Id = await ContentService.CreateCommentAsync(user2, user1.UserId, thread1Id, "Child", OperationContext.New());
        var thread3Id = await ContentService.CreateCommentAsync(user1, user2.UserId, thread2Id, "Grandchild", OperationContext.New());

        // Uses Change Feed
        await Task.Delay(2_000);
        
        var thread1 = await ContentService.GetThreadAsync(user1, user1.UserId, thread1Id, 5, OperationContext.New());
        Assert.That(thread1.CommentCount, Is.EqualTo(1));
        Assert.That(thread1.LastComments[0].CommentCount, Is.EqualTo(1));
        
        var thread2 = await ContentService.GetThreadAsync(user2, user2.UserId, thread2Id, 5, OperationContext.New());
        Assert.That(thread2.CommentCount, Is.EqualTo(1));
        Assert.That(thread2.LastComments[0].CommentCount, Is.EqualTo(0));
    }
    
    [Test, Order(1)]
    public async Task Content_Comment_RecoverFrom_ThreadPartialFailure()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var thread1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var context = OperationContext.New();
        context.FailOnSignal("create-comment-thread", CreateCosmoException());
        await ContentService.CreateCommentAsync(user2, user1.UserId, thread1Id, "Child", context);

        await Task.Delay(2_000);
        
        var thread  = await ContentService.GetThreadAsync(user1, user1.UserId, thread1Id, 5, OperationContext.New());
        var commentThread = await ContentService.GetThreadAsync(user2, user2.UserId, thread.LastComments[0].CommentId, 5, OperationContext.New());
        Assert.That(commentThread, Is.Not.Null);
        Assert.That(commentThread.Content, Is.EqualTo("Child"));
    }
    
    [Test, Order(2)]
    public async Task Content_Comment_RecoverFrom_CommentPartialFailure()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var thread1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var commentId = await ContentService.CreateCommentAsync(user2, user1.UserId, thread1Id, "Child", OperationContext.New());
        
        var context = OperationContext.New();
        context.FailOnSignal("update-comment", CreateCosmoException());
        await ContentService.UpdateThreadAsync(user2, commentId, "Child !!!", context);

        await Task.Delay(2_000);
        
        var commentThread = await ContentService.GetThreadAsync(user2, user2.UserId, commentId, 5, OperationContext.New());
        Assert.That(commentThread, Is.Not.Null);
        Assert.That(commentThread.Content, Is.EqualTo("Child !!!"));
        
        var thread  = await ContentService.GetThreadAsync(user1, user1.UserId, thread1Id, 5, OperationContext.New());
        Assert.That(thread.CommentCount, Is.EqualTo(1));
        Assert.That(thread.LastComments[0].Content, Is.EqualTo("Child !!!"));
    }
    
    [Test, Order(3)]
    public async Task Content_Can_RecoverDelete()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var thread1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var comment2Id = await ContentService.CreateCommentAsync(user2, user1.UserId, thread1Id, "Child", OperationContext.New());
        var comment1Id = await ContentService.CreateCommentAsync(user1, user1.UserId, thread1Id, "Child Reply !!!", OperationContext.New());

        Console.WriteLine("delete " + comment2Id);
        var context = OperationContext.New();
        context.FailOnSignal("delete-comment", CreateCosmoException());
        await ContentService.DeleteThreadAsync(user2, comment2Id, context);
        
        Assert.ThrowsAsync<ContentException>(() => ContentService.GetThreadAsync(user2, user2.UserId, comment2Id, 5, OperationContext.New()));

        await Task.Delay(5_000);
        
        var thread = await ContentService.GetThreadAsync(user1, user1.UserId, thread1Id, 5, OperationContext.New());
        Assert.That(thread, Is.Not.Null);
        Assert.That(thread.CommentCount, Is.EqualTo(1));
        Assert.That(thread.LastComments.Count, Is.EqualTo(1));
        Assert.That(thread.LastComments[^1].Content, Is.EqualTo("Child Reply !!!"));
    }
    

    [Order(4)]
    [TestCase(true)]
    [TestCase(false)]
    public async Task Content_Like_Recovers(bool like)
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var thread1Id = await ContentService.CreateThreadAsync(user1, "Root", OperationContext.New());
        var thread2Id = await ContentService.CreateCommentAsync(user2, user1.UserId, thread1Id, "Child", OperationContext.New());

        var context = OperationContext.New();
        context.FailOnSignal("react-thread", CreateCosmoException());
        await ContentService.ReactToThreadAsync(user1, user2.UserId, thread2Id, like, context);

        await Task.Delay(2_000);
        
        var thread2 = await ContentService.GetThreadAsync(user2, user2.UserId, thread2Id, 5, OperationContext.New());
        Assert.That(thread2.LikeCount, Is.EqualTo(like ? 1 : 0));

        var thread1 = await ContentService.GetThreadAsync(user1, user1.UserId, thread1Id, 5, OperationContext.New());
        Assert.That(thread1.LastComments[0].LikeCount, Is.EqualTo(like ? 1 : 0));
    }
}