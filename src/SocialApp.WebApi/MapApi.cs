using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SocialApp.Models.Account;
using SocialApp.Models.Session;
using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Services;
using SocialApp.WebApi.Features.Follow.Services;
using SocialApp.WebApi.Features.Session.Services;
using SocialApp.WebApi.Infrastructure;

namespace SocialApp.WebApi;

public static class MapApi
{
    public static void MapApiEndpoints(this WebApplication app)
    {
        MapAccount(app);
        MapSession(app);
        MapFollowers(app);
        
        app.MapGet("/health", () => "OK");
    }

    private static void MapAccount(WebApplication app)
    {
        app.MapPost("/register", async (RegisterRequest model, IAccountService accounts, CancellationToken cancel) =>
        {
            var context = new OperationContext(cancel);
            return await accounts.RegisterAsync(model, context);
        });
    }
    
    private static void MapSession(WebApplication app)
    {
        app.MapPost("/login", async (LoginRequest model, ISessionService sessions, HttpContext http) =>
        {
            var context = new OperationContext(http.RequestAborted);
            var session = await sessions.LoginWithPasswordAsync(model, context);
            
            if(session?.SessionId == null)
                throw new InvalidOperationException("No session was generated.");
            
            var claims = new List<Claim> { new(ClaimTypes.Sid, session.SessionId) };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);
        });
        
        app.MapPost("/logout", async (SessionGetter sessionGetter, ISessionService sessions, OperationContext context, HttpContext http) =>
        {
            var session = await sessionGetter.GetUserSessionAsync(context);
            await sessions.EndSessionAsync(session.SessionId, context);
            return http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }).RequireAuthorization();
    }
    
    private static void MapFollowers(WebApplication app)
    {
        app.MapGet("/followers/", async (SessionGetter sessionGetter, IUserHandleService handles, IFollowersService follows, OperationContext context) =>
        {
            var session = await sessionGetter.GetUserSessionAsync(context);
            return await handles.GetHandleFromUserIdsAsync(await follows.GetFollowersAsync(session.UserId, context), context);
        }).RequireAuthorization();
        
        app.MapGet("/follow/", async (SessionGetter sessionGetter, IUserHandleService handles, IFollowersService follows, OperationContext context) =>
        {
            var session = await sessionGetter.GetUserSessionAsync(context);
            return await handles.GetHandleFromUserIdsAsync(await follows.GetFollowingsAsync(session.UserId, context), context);
        }).RequireAuthorization();
        
        app.MapPost("/follow/{handle}", async ([FromRoute]string handle, SessionGetter sessionGetter, IUserHandleService handles, IFollowersService follows, OperationContext context) =>
        {
            var session = await sessionGetter.GetUserSessionAsync(context);
            var otherUserId = await handles.GetUserIdAsync(handle, context);
            await follows.FollowAsync(session.UserId, otherUserId, context);
            
        }).RequireAuthorization();
        
        app.MapDelete("/follow/{handle}", async ([FromRoute]string handle, SessionGetter sessionGetter, IUserHandleService handles, IFollowersService follows, OperationContext context) =>
        {
            var session = await sessionGetter.GetUserSessionAsync(context);
            var otherUserId = await handles.GetUserIdAsync(handle, context);
            await follows.UnfollowAsync(session.UserId, otherUserId, context);
        }).RequireAuthorization();
    }
}