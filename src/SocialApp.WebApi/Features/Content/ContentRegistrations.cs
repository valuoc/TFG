using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features._Shared.Tuples;
using SocialApp.WebApi.Features.Content.Queries;
using SocialApp.WebApi.Features.Content.Services;
using SocialApp.WebApi.Infrastructure.Jobs;

namespace SocialApp.WebApi.Features.Content;

public static class ContentRegistrations
{
    public static void RegisterContentServices(this IServiceCollection services)
    {
        services.AddHostedService<ContentProcessorJob>();
        services.AddSingleton<IContentService, ContentService>();
        services.AddSingleton<IFeedService, FeedService>();
        services.AddSingleton<IContentStreamProcessorService, ContentStreamProcessorService>();
        services.AddSingleton<IQueryMany<UserFeedQuery, FeedConversationTuple>, UserFeedQueryHandler>();
        
    }
}