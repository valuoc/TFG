using SocialApp.WebApi.Features.Account.Exceptions;
using SocialApp.WebApi.Features.Services;
using SocialApp.WebApi.Features.Session.Models;

namespace SocialApp.Tests.ServicesTests;

[Order(1)]
public class AccountServiceTests : ServiceTestsBase
{
    [Test, Order(1)]
    public async Task RegisterUser_ValidUser_RegistersUser()
    {
        var userName = Guid.NewGuid().ToString("N");
        var id1 = await AccountService.RegisterAsync($"{userName}@xxx.com", userName, "Display"+userName, "pass", OperationContext.None());
        Console.WriteLine(id1);
        var userName2 = Guid.NewGuid().ToString("N");
        var id2 = await AccountService.RegisterAsync($"{userName2}@xxx.com", userName2, "Display"+userName2, "pass", OperationContext.None());
        Console.WriteLine(id2);

        var profile = await AccountService.LoginWithPasswordAsync($"{userName}@xxx.com", "pass", OperationContext.None());
        var sessionId = await SessionService.StarSessionAsync(new UserSession(profile.UserId, profile.DisplayName, profile.Handle), OperationContext.None());
        Console.WriteLine(sessionId);
        var user = await SessionService.GetSessionAsync(sessionId, OperationContext.None());
        Console.WriteLine(user.UserId);
        Console.WriteLine(user);
        
        await SessionService.EndSessionAsync(sessionId, OperationContext.None());
        await SessionService.EndSessionAsync(sessionId, OperationContext.None());
    }

    [Test, Order(2)]
    public async Task RegisterUser_FailOnPending_CleansAfter()
    {
        var userName = Guid.NewGuid().ToString("N");
        var context = OperationContext.None();
        context.FailOnSignal("pending-account", CreateCosmoException());
        Assert.ThrowsAsync<AccountException>(async () => await AccountService.RegisterAsync($"{userName}@xxx.com", userName, "Display" + userName, "pass", context));

        var user = await AccountService.LoginWithPasswordAsync($"{userName}@xxx.com", "pass", OperationContext.None());
        Assert.IsNull(user);
        
        var deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.That(deleted, Is.EqualTo(0));
    }

    [Test, Order(3)]
    public async Task RegisterUser_FailDuplicateEmail_CleansAfter()
    {
        var context = OperationContext.None();
        var userName = Guid.NewGuid().ToString("N");
        await AccountService.RegisterAsync($"{userName}@xxx.com", userName, "Display" + userName, "pass", context);
            
        context = OperationContext.None();
        var error = Assert.ThrowsAsync<AccountException>(async () => await AccountService.RegisterAsync($"{userName}@xxx.com", userName, "Display" + userName, "pass2", context));
        Assert.That(error.Error, Is.EqualTo(AccountError.EmailAlreadyRegistered));
        
        var user = await AccountService.LoginWithPasswordAsync($"{userName}@xxx.com", "pass2", OperationContext.None());
        Assert.IsNull(user);
        
        var deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.That(deleted, Is.EqualTo(1));
    }
    
    [Test, Order(4)]
    public async Task RegisterUser_FailOnEmailLock_CleansAfter()
    {
        var context = OperationContext.None();
        var userName = Guid.NewGuid().ToString("N");
        context.FailOnSignal("email-lock", CreateCosmoException());
        Assert.ThrowsAsync<AccountException>(async () => await AccountService.RegisterAsync($"{userName}@xxx.com", userName, "Display" + userName, "pass", context));

        var user = await AccountService.LoginWithPasswordAsync($"{userName}@xxx.com", "pass", OperationContext.None());
        Assert.IsNull(user);
        
        var deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.That(deleted, Is.EqualTo(1));
    }
    
    [Test, Order(5)]
    public async Task RegisterUser_FailDuplicateHandle_CleansAfter()
    {
        var context = OperationContext.None();
        var userName = Guid.NewGuid().ToString("N");
        await AccountService.RegisterAsync($"{userName}@xxx.com", userName, "Display" + userName, "pass", context);
            
        context = OperationContext.None();
        var error = Assert.ThrowsAsync<AccountException>(async () => await AccountService.RegisterAsync($"{userName}@xxx2.com", userName, "Display" + userName, "pass", context));
        Assert.That(error.Error, Is.EqualTo(AccountError.HandleAlreadyRegistered));
        
        var user = await AccountService.LoginWithPasswordAsync($"{userName}@xxx2.com", "pass", OperationContext.None());
        Assert.IsNull(user);
        
        var deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.That(deleted, Is.EqualTo(1));
    }

    [Test, Order(6)]
    public async Task RegisterUser_FailOnHandleLock_CleansAfter()
    {
        var context = OperationContext.None();
        var userName = Guid.NewGuid().ToString("N");
        context.FailOnSignal("handle-lock", CreateCosmoException());
        Assert.ThrowsAsync<AccountException>(async () => await AccountService.RegisterAsync($"{userName}@xxx.com", userName, "Display" + userName, "pass", context));

        var user = await AccountService.LoginWithPasswordAsync($"{userName}@xxx.com", "pass", OperationContext.None());
        Assert.IsNull(user);
        
        var deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.That(deleted, Is.EqualTo(1));
    }
    
    [Test, Order(7)]
    public async Task RegisterUser_FailOnRest_CleansAfter()
    {
        string[] signals = ["user", "complete-email-lock", "complete-email-lock", "complete-user"];

        foreach (var signal in signals)
        {
            var context = OperationContext.None();
            var userName = Guid.NewGuid().ToString("N");
            context.FailOnSignal(signal, CreateCosmoException());
            Assert.ThrowsAsync<AccountException>(async () => await AccountService.RegisterAsync($"{userName}@xxx.com", userName, "Display" + userName, "pass", context));

            var user = await AccountService.LoginWithPasswordAsync($"{userName}@xxx.com", "pass", OperationContext.None());
            Assert.IsNull(user);
        
            var deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, CancellationToken.None);
            Assert.That(deleted, Is.EqualTo(1));
        }
    }
    
    [Test, Order(8)]
    public async Task RegisterUser_FailOnCleanPending_AllowContinue()
    {
        var context = OperationContext.None();
        var userName = Guid.NewGuid().ToString("N");
        context.FailOnSignal("complete-pending-account", CreateCosmoException());
        var accountId = await AccountService.RegisterAsync($"{userName}@xxx.com", userName, "Display" + userName, "pass", context);
        Assert.That(accountId, Is.Not.Null);
        
        var user = await AccountService.LoginWithPasswordAsync($"{userName}@xxx.com", "pass", OperationContext.None());
        Assert.IsNotNull(user);
        
        var deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, CancellationToken.None);
        Assert.That(deleted, Is.EqualTo(0));
    }
}