using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SocialApp.Models.Account;
using SocialApp.Models.Content;
using SocialApp.Models.Session;
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
    private static  async Task<(UserSession? session, IResult? problem)> GetUserIdOrProblemAsync(SessionGetter sessions, OperationContext context)
    {
        var session = await sessions.GetUserSessionAsync(context);
        if (session == null)
        {
            return (null, Results.Unauthorized());
        }
        else
        {
            return (session, null);
        }
    }
    
    public static void MapApiEndpoints(this WebApplication app)
    {
        MapAccount(app);
        MapSession(app);
        MapFollowers(app);
        MapContent(app);
        
        app.MapGet("/health", () => "OK");
    }

    private static void MapContent(WebApplication app)
    {
        var group = app.MapGroup("/conversation").RequireAuthorization();

        group.MapPost("/", async (ContentRequest request, SessionGetter sessionGetter, IContentService contents, OperationContext context, HttpContext http) =>
        {
            var (session, problem) = await GetUserIdOrProblemAsync(sessionGetter, context);
            if (problem != null)
                return problem;
            var conversationId = await contents.StartConversationAsync(session!, request.Content, context);
            http.Response.Headers.Location = $"/conversation/{session!.Handle}/{conversationId}";
            return Results.Ok();
        });
            
        group.MapGet("/{handle}/{conversationId}", async (string handle, string conversationId, SessionGetter sessionGetter, IUserHandleService handles, IContentService contents, OperationContext context) =>
        {
            var conversationUserId = await handles.GetUserIdAsync(handle, context);
            return Results.Ok(await contents.GetConversationAsync(conversationUserId, conversationId, 5, context));
        }).AllowAnonymous();
        
        group.MapGet("/{handle}/{conversationId}/comments", async (string handle, string conversationId, string? before, SessionGetter sessionGetter, IUserHandleService handles, IContentService contents, OperationContext context) =>
        {
            var conversationUserId = await handles.GetUserIdAsync(handle, context);
            return Results.Ok(await contents.GetPreviousCommentsAsync(conversationUserId, conversationId, before, 5, context));
        }).AllowAnonymous();
        
        group.MapPut("/{handle}/{conversationId}", async (string handle, string conversationId, ContentRequest request, SessionGetter sessionGetter, IContentService contents, OperationContext context) =>
        {
            var (session, problem) = await GetUserIdOrProblemAsync(sessionGetter, context);
            if (problem != null)
                return problem;
            if (session!.Handle != handle)
                return Results.Unauthorized();
            await contents.UpdateConversationAsync(session, conversationId, request.Content, context);
            return Results.Ok();
        });
        
        group.MapPut("/{handle}/{conversationId}/like", async (string handle, string conversationId, ReactContent request, SessionGetter sessionGetter, IUserHandleService handles, IContentService contents, OperationContext context) =>
        {
            var (session, problem) = await GetUserIdOrProblemAsync(sessionGetter, context);
            if (problem != null)
                return problem;
            var conversationUserId = await handles.GetUserIdAsync(handle, context);
            await contents.ReactToConversationAsync(session!, conversationUserId, conversationId, request.Like, context);
            return Results.Ok();
        });
        
        group.MapDelete("/{handle}/{conversationId}", async (string handle, string conversationId, SessionGetter sessionGetter, IContentService contents, OperationContext context) =>
        {
            var (session, problem) = await GetUserIdOrProblemAsync(sessionGetter, context);
            if (problem != null)
                return problem;
            if (session!.Handle != handle)
                return Results.Unauthorized();
            await contents.DeleteConversationAsync(session!, conversationId, context);
            return Results.Ok();
        });
        
        group.MapPost("/{handle}/{conversationId}", async (string handle, string conversationId, SessionGetter sessionGetter, ContentRequest request, IUserHandleService handles, IContentService contents, OperationContext context, HttpContext http) =>
        {
            var (session, problem) = await GetUserIdOrProblemAsync(sessionGetter, context);
            if (problem != null)
                return problem;
            var conversationUserId = await handles.GetUserIdAsync(handle, context);
            var commentId = await contents.CommentAsync(session!, conversationUserId, conversationId, request.Content, context);
            http.Response.Headers.Location = $"/conversation/{handle}/{conversationId}/comments/{commentId}";
            return Results.Ok();
        });
        
        group.MapGet("/{handle}", async (string handle, string? before, IUserHandleService handles, IContentService contents, OperationContext context) =>
        {
            var conversationUserId = await handles.GetUserIdAsync(handle, context);
            return Results.Ok(await contents.GetUserConversationsAsync(conversationUserId, before, 10, context));
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
        
        app.MapPost("/logout", async (SessionGetter sessionGetter, ISessionService sessions, OperationContext context, HttpContext http) =>
        {
            var (session, problem) = await GetUserIdOrProblemAsync(sessionGetter, context);
            if (problem != null)
                return problem;
            await sessions.EndSessionAsync(session.SessionId, context);
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok();
        }).RequireAuthorization();
    }
    
    private static void MapFollowers(WebApplication app)
    {
        app.MapGet("/followers/", async (SessionGetter sessionGetter, IUserHandleService handles, IFollowersService follows, OperationContext context) =>
        {
            var (session, problem) = await GetUserIdOrProblemAsync(sessionGetter, context);
            if (problem != null)
                return problem;
            return Results.Ok(await handles.GetHandleFromUserIdsAsync(await follows.GetFollowersAsync(session.UserId, context), context));
        }).RequireAuthorization();
        
        app.MapGet("/follow/", async (SessionGetter sessionGetter, IUserHandleService handles, IFollowersService follows, OperationContext context) =>
        {
            var (session, problem) = await GetUserIdOrProblemAsync(sessionGetter, context);
            if (problem != null)
                return problem;
            return Results.Ok(await handles.GetHandleFromUserIdsAsync(await follows.GetFollowingsAsync(session.UserId, context), context));
        }).RequireAuthorization();
        
        app.MapPost("/follow/{handle}", async ([FromRoute]string handle, SessionGetter sessionGetter, IUserHandleService handles, IFollowersService follows, OperationContext context) =>
        {
            var (session, problem) = await GetUserIdOrProblemAsync(sessionGetter, context);
            if (problem != null)
                return problem;
            var otherUserId = await handles.GetUserIdAsync(handle, context);
            await follows.FollowAsync(session.UserId, otherUserId, context);
            return Results.Ok();
        }).RequireAuthorization();
        
        app.MapDelete("/follow/{handle}", async ([FromRoute]string handle, SessionGetter sessionGetter, IUserHandleService handles, IFollowersService follows, OperationContext context) =>
        {
            var (session, problem) = await GetUserIdOrProblemAsync(sessionGetter, context);
            if (problem != null)
                return problem;
            var otherUserId = await handles.GetUserIdAsync(handle, context);
            await follows.UnfollowAsync(session.UserId, otherUserId, context);
            return Results.Ok();
        }).RequireAuthorization();
    }
}