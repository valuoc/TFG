using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Exceptions;

namespace SocialApp.Tests.ServicesTests;

[Order(4)]
public class ContentServiceTests: ServiceTestsBase
{
    [Test, Order(1)]
    public async Task Content_Conversation_CanBe_Added()
    {
        var user1 = await CreateUserAsync();

        var context = OperationContext.New();
        var conversation1Id = await ContentService.StartConversationAsync(user1, "This is a conversation.", context);
        Console.WriteLine(context.OperationCharge);

        context = OperationContext.New();
        var conversation1 = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, context);
        Console.WriteLine(context.OperationCharge);

        Assert.That(conversation1, Is.Not.Null);
        Assert.That(conversation1.ViewCount, Is.EqualTo(1));
        Assert.That(conversation1.CommentCount, Is.EqualTo(0));
        Assert.That(conversation1.Content, Is.EqualTo("This is a conversation."));
    }
    
    [Test, Order(2)]
    public async Task Content_Comment_CanBe_Added()
    {
        var user1 = await CreateUserAsync();

        var conversation1Id = await ContentService.StartConversationAsync(user1, "This is a conversation.", OperationContext.New());
        
        var user2 = await CreateUserAsync();
        
        var commentId = await ContentService.CommentAsync(user2, user1.UserId, conversation1Id, "This is a comment.", OperationContext.New());
        
        // Comment is a conversation on its own
        var conversation2 = await ContentService.GetConversationAsync(user2, user2.UserId, commentId, 5, OperationContext.New());
        Assert.That(conversation2, Is.Not.Null);
        Assert.That(conversation2.ViewCount, Is.EqualTo(1));
        Assert.That(conversation2.CommentCount, Is.EqualTo(0));
        Assert.That(conversation2.Content, Is.EqualTo("This is a comment."));
        
        // Comment appears as comment
        var conversation1 = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, OperationContext.New());
        Assert.That(conversation1, Is.Not.Null);
        Assert.That(conversation1.CommentCount, Is.EqualTo(1));
        Assert.That(conversation1.Content, Is.EqualTo("This is a conversation."));
        
        commentId = await ContentService.CommentAsync(user1, user1.UserId, conversation1Id, "This is a self-reply.", OperationContext.New());
        
        conversation1 = await ContentService.GetConversationAsync(user1, user1.UserId, commentId, 5, OperationContext.New());
        Assert.That(conversation1, Is.Not.Null);
        Assert.That(conversation1.ViewCount, Is.EqualTo(1));
        Assert.That(conversation1.CommentCount, Is.EqualTo(0));
        Assert.That(conversation1.Content, Is.EqualTo("This is a self-reply."));
        
        conversation1 = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, OperationContext.New());
        Assert.That(conversation1, Is.Not.Null);
        Assert.That(conversation1.ViewCount, Is.EqualTo(2));
        Assert.That(conversation1.CommentCount, Is.EqualTo(2));
        Assert.That(conversation1.Content, Is.EqualTo("This is a conversation."));
        Assert.That(conversation1.LastComments[^1].Content, Is.EqualTo("This is a self-reply."));
    }

    [Test, Order(3)]
    public async Task Content_Comment_Is_SortedAndPaginated()
    {
        var now = DateTimeOffset.UtcNow;
        
        var user1 = await CreateUserAsync();

        var context = new OperationContext(CancellationToken.None);
        context.SetTime(now);
        var conversation1Id = await ContentService.StartConversationAsync(user1, "This is a conversation.", context);

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
            await ContentService.CommentAsync(moment%2==0?user2:user3, user1.UserId, conversation1Id, moment.ToString(), context);
        }

        context = OperationContext.New();
        var conversation = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, context);
        Console.WriteLine(context.OperationCharge);
        Assert.That(conversation, Is.Not.Null);
        Assert.That(conversation.LastComments.Count, Is.EqualTo(5));
        Assert.That(conversation.CommentCount, Is.EqualTo(10));
        for (var i = 0; i < 5; i++)
            Assert.That(conversation.LastComments[i].Content, Is.EqualTo((i+6).ToString()));
        
        var prevComments = await ContentService.GetPreviousCommentsAsync(user1, user1.UserId, conversation1Id, conversation.LastComments[0].CommentId, 2, OperationContext.New());
        Assert.That(prevComments, Is.Not.Null);
        Assert.That(prevComments.Count, Is.EqualTo(2));
        for (var i = 0; i < 2; i++)
            Assert.That(prevComments[i].Content, Is.EqualTo((i+4).ToString()));
        
        prevComments = await ContentService.GetPreviousCommentsAsync(user1, user1.UserId, conversation1Id, prevComments[0].CommentId, 3, OperationContext.New());
        Assert.That(prevComments, Is.Not.Null);
        Assert.That(prevComments.Count, Is.EqualTo(3));
        for (var i = 0; i < 3; i++)
            Assert.That(prevComments[i].Content, Is.EqualTo((i+1).ToString()));
        
        prevComments = await ContentService.GetPreviousCommentsAsync(user1, user1.UserId, conversation1Id, prevComments[0].CommentId, 3, OperationContext.New());
        Assert.That(prevComments, Is.Not.Null);
        Assert.That(prevComments, Is.Empty);
    }
    
    
    [Test, Order(5)]
    public async Task Content_Paginate_Conversations()
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
            var conversation1Id = await ContentService.StartConversationAsync(user1, moment.ToString(), context);

            if (i % 3 == 0)
            {
                var conversation12d = await ContentService.StartConversationAsync(user2, moment.ToString(), context);
                await ContentService.CommentAsync(user1, user2.UserId, conversation12d, moment + "reply!!", context);
            }
        }

        context = OperationContext.New();
        var conversations = await ContentService.GetUserConversationsAsync(user1, null, 5, context);
        Console.WriteLine(context.OperationCharge);
        Assert.That(conversations, Is.Not.Null);
        Assert.That(conversations.Count, Is.EqualTo(5));
        for (var i = 0; i < 5; i++)
            Assert.That(conversations[i].Content, Is.EqualTo((10 - i).ToString()));
        
        conversations = await ContentService.GetUserConversationsAsync(user1, conversations[^1].ConversationId, 5, OperationContext.New());
        Assert.That(conversations, Is.Not.Null);
        Assert.That(conversations.Count, Is.EqualTo(5));
        for (var i = 0; i < 5; i++)
            Assert.That(conversations[i].Content, Is.EqualTo((5 - i).ToString()));
        
        conversations = await ContentService.GetUserConversationsAsync(user1, conversations[^1].ConversationId, 5, OperationContext.New());
        Assert.That(conversations, Is.Not.Null);
        Assert.That(conversations.Count, Is.EqualTo(0));
    }
    
    [Test, Order(6)]
    public async Task Content_Can_Update()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var conversation1Id = await ContentService.StartConversationAsync(user1, "Root", OperationContext.New());
        var conversation2Id = await ContentService.CommentAsync(user2, user1.UserId, conversation1Id, "Child", OperationContext.New());
        await ContentService.UpdateConversationAsync(user2, conversation2Id, "Updated!", OperationContext.New());
        
        var conversation1 = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, OperationContext.New());
        Assert.That(conversation1.LastComments[0].Content, Is.EqualTo("Updated!"));
        
        var conversation2 = await ContentService.GetConversationAsync(user2, user2.UserId, conversation2Id, 5, OperationContext.New());
        Assert.That(conversation2.Content, Is.EqualTo("Updated!"));
    }
    
    [Test, Order(7)]
    public async Task Content_Can_Delete()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var conversation1Id = await ContentService.StartConversationAsync(user1, "Root", OperationContext.New());
        var comment2Id = await ContentService.CommentAsync(user2, user1.UserId, conversation1Id, "Child", OperationContext.New());
        var comment1Id = await ContentService.CommentAsync(user1, user1.UserId, conversation1Id, "Child Reply !!!", OperationContext.New());

        var conversation = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, OperationContext.New());
        Assert.That(conversation, Is.Not.Null);
        Assert.That(conversation.CommentCount, Is.EqualTo(2));
        Assert.That(conversation.LastComments.Count, Is.EqualTo(2));
        Assert.That(conversation.LastComments[^1].Content, Is.EqualTo("Child Reply !!!"));

        var context = OperationContext.New();
        await ContentService.DeleteConversationAsync(user2, comment2Id, context);
        Console.WriteLine(context.OperationCharge);
        Console.WriteLine(context.DebugMetrics);
        
        Assert.ThrowsAsync<ContentException>(() => ContentService.GetConversationAsync(user2, user2.UserId, comment2Id, 5, OperationContext.New()));
        
        conversation = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, OperationContext.New());
        Assert.That(conversation, Is.Not.Null);
        Assert.That(conversation.CommentCount, Is.EqualTo(1));
        Assert.That(conversation.LastComments.Count, Is.EqualTo(1));
        Assert.That(conversation.LastComments[^1].Content, Is.EqualTo("Child Reply !!!"));
    }
    
    [Test, Order(8)]
    public async Task Content_Like_Populate()
    {
        var user1 = await CreateUserAsync();
        var user2 = await CreateUserAsync();

        var conversation1Id = await ContentService.StartConversationAsync(user1, "Root", OperationContext.New());
        var conversation2Id = await ContentService.CommentAsync(user2, user1.UserId, conversation1Id, "Child", OperationContext.New());

        await ContentService.ReactToConversationAsync(user1, user2.UserId, conversation2Id, false, OperationContext.New());

        var conversation2 = await ContentService.GetConversationAsync(user2, user2.UserId, conversation2Id, 5, OperationContext.New());
        Assert.That(conversation2.LikeCount, Is.EqualTo(0));

        var conversation1 = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, OperationContext.New());
        Assert.That(conversation1.LastComments[0].LikeCount, Is.EqualTo(0));
        
        for (var i = 0; i < 2; i++)
        {
            await ContentService.ReactToConversationAsync(user1, user2.UserId, conversation2Id, true, OperationContext.New());

            conversation2 = await ContentService.GetConversationAsync(user2, user2.UserId, conversation2Id, 5, OperationContext.New());
            Assert.That(conversation2.LikeCount, Is.EqualTo(1));

            conversation1 = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, OperationContext.New());
            Assert.That(conversation1.LastComments[0].LikeCount, Is.EqualTo(1));
        }
        
        for (var i = 0; i < 2; i++)
        {
            await ContentService.ReactToConversationAsync(user1, user2.UserId, conversation2Id, false, OperationContext.New());

            conversation2 = await ContentService.GetConversationAsync(user2, user2.UserId, conversation2Id, 5, OperationContext.New());
            Assert.That(conversation2.LikeCount, Is.EqualTo(0));

            conversation1 = await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, OperationContext.New());
            Assert.That(conversation1.LastComments[0].LikeCount, Is.EqualTo(0));
        }
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
        var conversation1Id = await ContentService.StartConversationAsync(user1, "X".PadLeft(size, 'X'), context);
        Console.WriteLine(context.OperationCharge);
        writeRcu += context.OperationCharge;
        
        context = new OperationContext(CancellationToken.None);
        context.SetTime(now);
        await ContentService.UpdateConversationAsync(user1, conversation1Id, "Y".PadLeft(size, 'Y'), context);
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
            await ContentService.CommentAsync(moment%2==0?user2:user3, user1.UserId, conversation1Id, "X".PadLeft(size, 'X'), context);
            Console.WriteLine(context.OperationCharge);
            writeRcu += context.OperationCharge;
        }

        context = OperationContext.New();
        await ContentService.GetConversationAsync(user1, user1.UserId, conversation1Id, 5, context);
        Console.WriteLine(context.OperationCharge);
        writeRcu += context.OperationCharge;
        Console.WriteLine("TOTAL");
        Console.WriteLine(writeRcu);
    }
}