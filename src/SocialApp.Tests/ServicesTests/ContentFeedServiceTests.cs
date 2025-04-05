using System.Threading.Tasks;
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

        var conversation1Id = await ContentService.StartConversationAsync(user1, "Root", OperationContext.New());
        var conversation2Id = await ContentService.CommentAsync(user2, user1.UserId, conversation1Id, "Child", OperationContext.New());
        var conversation3Id = await ContentService.CommentAsync(user1, user2.UserId, conversation2Id, "Grandchild", OperationContext.New());

        // Uses Change Feed
        await Task.Delay(2_000);
        
        var conversation1 = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, OperationContext.New());
        Assert.That(conversation1.CommentCount, Is.EqualTo(1));
        Assert.That(conversation1.LastComments[0].CommentCount, Is.EqualTo(1));
        
        var conversation2 = await ContentService.GetConversationAsync(user2, user2.UserId, conversation2Id, 5, OperationContext.New());
        Assert.That(conversation2.CommentCount, Is.EqualTo(1));
        Assert.That(conversation2.LastComments[0].CommentCount, Is.EqualTo(0));
    }
    
    [Test, Order(1)]
    public async Task Content_Comment_RecoverFrom_ConversationPartialFailure()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var conversation1Id = await ContentService.StartConversationAsync(user1, "Root", OperationContext.New());
        var context = OperationContext.New();
        context.FailOnSignal("create-comment-conversation", CreateCosmoException());
        await ContentService.CommentAsync(user2, user1.UserId, conversation1Id, "Child", context);

        await Task.Delay(2_000);
        
        var conversation  = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, OperationContext.New());
        var commentConversation = await ContentService.GetConversationAsync(user2, user2.UserId, conversation.LastComments[0].CommentId, 5, OperationContext.New());
        Assert.That(commentConversation, Is.Not.Null);
        Assert.That(commentConversation.Content, Is.EqualTo("Child"));
    }
    
    [Test, Order(2)]
    public async Task Content_Comment_RecoverFrom_CommentPartialFailure()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var conversation1Id = await ContentService.StartConversationAsync(user1, "Root", OperationContext.New());
        var commentId = await ContentService.CommentAsync(user2, user1.UserId, conversation1Id, "Child", OperationContext.New());
        
        var context = OperationContext.New();
        context.FailOnSignal("update-comment", CreateCosmoException());
        await ContentService.UpdateConversationAsync(user2, commentId, "Child !!!", context);

        await Task.Delay(2_000);
        
        var commentConversation = await ContentService.GetConversationAsync(user2, user2.UserId, commentId, 5, OperationContext.New());
        Assert.That(commentConversation, Is.Not.Null);
        Assert.That(commentConversation.Content, Is.EqualTo("Child !!!"));
        
        var conversation  = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, OperationContext.New());
        Assert.That(conversation.CommentCount, Is.EqualTo(1));
        Assert.That(conversation.LastComments[0].Content, Is.EqualTo("Child !!!"));
    }
    
    [Test, Order(3)]
    public async Task Content_Can_RecoverDelete()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var conversation1Id = await ContentService.StartConversationAsync(user1, "Root", OperationContext.New());
        var comment2Id = await ContentService.CommentAsync(user2, user1.UserId, conversation1Id, "Child", OperationContext.New());
        var comment1Id = await ContentService.CommentAsync(user1, user1.UserId, conversation1Id, "Child Reply !!!", OperationContext.New());

        Console.WriteLine("delete " + comment2Id);
        var context = OperationContext.New();
        context.FailOnSignal("delete-comment", CreateCosmoException());
        await ContentService.DeleteConversationAsync(user2, comment2Id, context);
        
        Assert.ThrowsAsync<ContentException>(() => ContentService.GetConversationAsync(user2, user2.UserId, comment2Id, 5, OperationContext.New()));

        await Task.Delay(5_000);
        
        var conversation = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, OperationContext.New());
        Assert.That(conversation, Is.Not.Null);
        Assert.That(conversation.CommentCount, Is.EqualTo(1));
        Assert.That(conversation.LastComments.Count, Is.EqualTo(1));
        Assert.That(conversation.LastComments[^1].Content, Is.EqualTo("Child Reply !!!"));
    }
    

    [Order(4)]
    [TestCase(true)]
    [TestCase(false)]
    public async Task Content_Like_Recovers(bool like)
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var conversation1Id = await ContentService.StartConversationAsync(user1, "Root", OperationContext.New());
        var conversation2Id = await ContentService.CommentAsync(user2, user1.UserId, conversation1Id, "Child", OperationContext.New());

        var context = OperationContext.New();
        context.FailOnSignal("react-conversation", CreateCosmoException());
        await ContentService.ReactToConversationAsync(user1, user2.UserId, conversation2Id, like, context);

        await Task.Delay(2_000);
        
        var conversation2 = await ContentService.GetConversationAsync(user2, user2.UserId, conversation2Id, 5, OperationContext.New());
        Assert.That(conversation2.LikeCount, Is.EqualTo(like ? 1 : 0));

        var conversation1 = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, OperationContext.New());
        Assert.That(conversation1.LastComments[0].LikeCount, Is.EqualTo(like ? 1 : 0));
    }
}