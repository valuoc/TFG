using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Content.Containers;
using SocialApp.WebApi.Features.Content.Models;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.WebApi.Features.Content.Services;

public sealed class FeedService
{
    private readonly UserDatabase _userDb;
    
    public FeedService(UserDatabase userDb)
        => _userDb = userDb;

    private FeedContainer GetFeedContainer()
        => new(_userDb);
    
    public async Task<IReadOnlyList<Post>> GetFeedAsync(UserSession session, OperationContext context)
    {
        var feeds = GetFeedContainer();
        var documents = await feeds.GetUserFeedAsync(session.UserId, null, 10, context);
        
        var postsModels = new List<Post>(documents.Count);
        foreach (var (postDoc, countsDoc) in documents)
        {
            var post = Post.From(postDoc);
            post.CommentCount = countsDoc.CommentCount;
            post.ViewCount = countsDoc.ViewCount;
            post.LikeCount = countsDoc.LikeCount;
            postsModels.Add(post);
        }
        return postsModels;
    }
}