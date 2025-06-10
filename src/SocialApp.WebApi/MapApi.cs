using System.Security.Claims;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SocialApp.Models.Account;
using SocialApp.Models.Content;
using SocialApp.Models.Session;
using SocialApp.WebApi.Data.User;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Services;
using SocialApp.WebApi.Features.Content.Services;
using SocialApp.WebApi.Features.Follow.Services;
using SocialApp.WebApi.Features.Session.Models;
using SocialApp.WebApi.Features.Session.Services;
using SocialApp.WebApi.Infrastructure;

namespace SocialApp.WebApi;

public static class MapApi
{
    private static  async Task<(UserSession? session, IResult? problem)> LoadSessionOrProblemAsync(SessionManager sessions, OperationContext context)
    {
        var session = await sessions.LoadUserSessionAsync(context);
        if (session == null)
        {
            return (null, Results.Unauthorized());
        }
        else
        {
            return (session, null);
        }
    }
    
    private static  async Task<UserSession?> TryLoadSessionAsync(SessionManager sessionManager, OperationContext context)
    {
        return await sessionManager.LoadUserSessionAsync(context);
    }
    
    public static void MapApiEndpoints(this WebApplication app)
    {
        MapAccount(app);
        MapSession(app);
        MapFollowers(app);
        MapContent(app);
        MapFeed(app);
        
        app.MapGet("/health", GetStatusString);
    }

    private static void MapContent(WebApplication app)
    {
        var group = app.MapGroup("/conversation").RequireAuthorization();

        group.MapPost("/", async (ContentRequest request, SessionManager sessionManager, IContentService contents, OperationContext context, HttpContext http) =>
        {
            var (session, problem) = await LoadSessionOrProblemAsync(sessionManager, context);
            try
            {
                if (problem != null)
                    return problem;
                var conversationId = await contents.StartConversationAsync(session!, request.Content, context);
                http.Response.Headers.Location = $"/conversation/{session!.Handle}/{conversationId}";
                return Results.Ok();
            }
            finally
            {
                await sessionManager.UpdateSessionAsync(session, context);
            }
        });
            
        group.MapGet("/{handle}/{conversationId}", async (string handle, string conversationId, SessionManager sessionManager, IContentService contents, OperationContext context) =>
        {
            var session = await TryLoadSessionAsync(sessionManager, context);
            try
            {
                return Results.Ok(await contents.GetConversationAsync(handle, conversationId, 5, context));
            }
            finally
            {
                await sessionManager.UpdateSessionAsync(session, context);
            }
        }).AllowAnonymous();
        
        group.MapGet("/{handle}/{conversationId}/comments", async (string handle, string conversationId, string? before, SessionManager sessionManager, IContentService contents, OperationContext context) =>
        {
            var session = await TryLoadSessionAsync(sessionManager, context);
            try
            {
                return Results.Ok(await contents.GetPreviousCommentsAsync(handle, conversationId, before, 5, context));
            }
            finally
            {
                await sessionManager.UpdateSessionAsync(session, context);
            }
        }).AllowAnonymous();
        
        group.MapPut("/{handle}/{conversationId}", async (string handle, string conversationId, ContentRequest request, SessionManager sessionManager, IContentService contents, OperationContext context) =>
        {
            var (session, problem) = await LoadSessionOrProblemAsync(sessionManager, context);
            try
            {
                if (problem != null)
                    return problem;
                if (session!.Handle != handle)
                    return Results.Unauthorized();
                await contents.UpdateConversationAsync(session, conversationId, request.Content, context);
                return Results.Ok();
            }
            finally
            {
                await sessionManager.UpdateSessionAsync(session, context);
            }
        });
        
        group.MapPut("/{handle}/{conversationId}/like", async (string handle, string conversationId, ReactRequest request, SessionManager sessionManager, IContentService contents, OperationContext context) =>
        {
            var (session, problem) = await LoadSessionOrProblemAsync(sessionManager, context);
            try
            {
                if (problem != null)
                    return problem;
                await contents.ReactToConversationAsync(session!, handle, conversationId, request.Like, context);
                return Results.Ok();
            }
            finally
            {
                await sessionManager.UpdateSessionAsync(session, context);
            }
        });
        
        group.MapDelete("/{handle}/{conversationId}", async (string handle, string conversationId, SessionManager sessionManager, IContentService contents, OperationContext context) =>
        {
            var (session, problem) = await LoadSessionOrProblemAsync(sessionManager, context);
            try
            {
                if (problem != null)
                    return problem;
                if (session!.Handle != handle)
                    return Results.Unauthorized();
                await contents.DeleteConversationAsync(session!, conversationId, context);
                return Results.Ok();
            }
            finally
            {
                await sessionManager.UpdateSessionAsync(session, context);
            }
        });
        
        group.MapPost("/{handle}/{conversationId}", async (string handle, string conversationId, SessionManager sessionManager, ContentRequest request, IContentService contents, OperationContext context, HttpContext http) =>
        {
            var (session, problem) = await LoadSessionOrProblemAsync(sessionManager, context);
            try
            {
                if (problem != null)
                    return problem;
                var commentId = await contents.CommentAsync(session!, handle, conversationId, request.Content, context);
                http.Response.Headers.Location = $"/conversation/{handle}/{conversationId}/comments/{commentId}";
                return Results.Ok();
            }
            finally
            {
                await sessionManager.UpdateSessionAsync(session, context);
            }
        });
        
        group.MapGet("/{handle}", async (string handle, string? before, SessionManager sessionManager, IContentService contents, OperationContext context) =>
        {
            var session = await TryLoadSessionAsync(sessionManager, context);
            try
            {
                return Results.Ok(await contents.GetUserConversationsAsync(handle, before, 10, context));
            }
            finally
            {
                await sessionManager.UpdateSessionAsync(session, context);
            }
        }).AllowAnonymous();
    }
    
    private static void MapAccount(WebApplication app)
    {
        app.MapPost("/register", async (RegisterRequest model, IAccountService accounts, OperationContext context) =>
        {
            await accounts.RegisterAsync(model, context);
            return Results.Ok();
        });
    }
    
    private static void MapFeed(WebApplication app)
    {
        app.MapGet("/feed", async (string? before, SessionManager sessionManager, IFeedService feeds, OperationContext context) =>
        {
            var (session, problem) = await LoadSessionOrProblemAsync(sessionManager, context);
            try
            {
                if (problem != null)
                    return problem;
                var posts = await feeds.GetFeedAsync(session!, before, context);
                return Results.Ok(posts);
            }
            finally
            {
                await sessionManager.UpdateSessionAsync(session, context);
            }
        }).RequireAuthorization();
    }
    
    private static void MapSession(WebApplication app)
    {
        app.MapPost("/login", async (LoginRequest model, ISessionService sessions, HttpContext http, OperationContext context) =>
        {
            var session = await sessions.LoginWithPasswordAsync(model, context);
            
            if(session?.SessionId == null)
                throw new InvalidOperationException("No session was generated.");
            
            var claims = new List<Claim> { new(ClaimTypes.Sid, session.SessionId) };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);
            return Results.Ok();
        });
        
        app.MapPost("/logout", async (SessionManager sessionManager, ISessionService sessions, OperationContext context, HttpContext http) =>
        {
            var (session, problem) = await LoadSessionOrProblemAsync(sessionManager, context);
            if (problem != null)
                return problem;
            await sessions.EndSessionAsync(session!, context);
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok();
        }).RequireAuthorization();
    }
    
    private static void MapFollowers(WebApplication app)
    {
        app.MapGet("/followers/", async (SessionManager sessionManager, IFollowersService follows, OperationContext context) =>
        {
            var (session, problem) = await LoadSessionOrProblemAsync(sessionManager, context);
            try
            {
                if (problem != null)
                    return problem;
                return Results.Ok(await follows.GetFollowersAsync(session!, context));
            }
            finally
            {
                await sessionManager.UpdateSessionAsync(session, context);
            }
        }).RequireAuthorization();
        
        app.MapGet("/follow/", async (SessionManager sessionManager, IFollowersService follows, OperationContext context) =>
        {
            var (session, problem) = await LoadSessionOrProblemAsync(sessionManager, context);
            try
            {
                if (problem != null)
                    return problem;
                return Results.Ok(await follows.GetFollowingsAsync(session!, context));
            }
            finally
            {
                await sessionManager.UpdateSessionAsync(session, context);
            }
        }).RequireAuthorization();
        
        app.MapPost("/follow/{handle}", async ([FromRoute]string handle, SessionManager sessionManager, IFollowersService follows, OperationContext context) =>
        {
            var (session, problem) = await LoadSessionOrProblemAsync(sessionManager, context);
            try
            {
                if (problem != null)
                    return problem;
                await follows.FollowAsync(session!, handle, context);
                return Results.Ok();
            }
            finally
            {
                await sessionManager.UpdateSessionAsync(session, context);
            }
        }).RequireAuthorization();
        
        app.MapDelete("/follow/{handle}", async ([FromRoute]string handle, SessionManager sessionManager, IFollowersService follows, OperationContext context) =>
        {
            var (session, problem) = await LoadSessionOrProblemAsync(sessionManager, context);
            try
            {
                if (problem != null)
                    return problem;
                await follows.UnfollowAsync(session!, handle, context);
                return Results.Ok();
            }
            finally
            {
                await sessionManager.UpdateSessionAsync(session, context);
            }
        }).RequireAuthorization();
    }
    
    private static JsonObject GetStatusString(HttpContext context, IConfiguration configuration)
    {
        var obj = new JsonObject();
        obj.Add("status", "OK");
        obj.Add("date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
        obj.Add("version", configuration.GetValue("IMAGE_TAG",string.Empty));
        obj.Add("environment", configuration.GetValue("ENVIRONMENT",string.Empty));
        obj.Add("region", configuration.GetValue("REGION",string.Empty));
        obj.Add("config", configuration.GetValue("ConfigurationSource","None"));
        obj.Add("user_container", GetUserContainer(context, configuration));
        return obj;
    }

    private static string GetUserContainer(HttpContext context, IConfiguration c)
    {
        context.RequestServices.GetRequiredService<UserDatabase>().GetContainer("contents");
        return "yes";
    }
}