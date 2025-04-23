using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Exceptions;

namespace SocialApp.Tests.ServicesTests;

[Order(2)]
public class AccountServiceTests : ServiceTestsBase
{
    [Test, Order(1)]
    public async Task RegisterUser_ValidUser_RegistersUser()
    {
        var userName = "t1" + Guid.NewGuid().ToString("N");
        var context = OperationContext.New();
        var id1 = await AccountService.RegisterAsync(new ($"{userName}@xxx.com", userName, "Display"+userName, "pass"), context);
        Console.WriteLine($"Cost of registering a player: {context.OperationCharge}");
        
        var userName2 = "t1" + Guid.NewGuid().ToString("N");
        var id2 = await AccountService.RegisterAsync(new ($"{userName2}@xxx.com", userName2, "Display"+userName2, "pass"), OperationContext.New());
        
        var session = await SessionService.LoginWithPasswordAsync(new ($"{userName}@xxx.com", "pass"), OperationContext.New());

        var user = await SessionService.GetSessionAsync(session.SessionId, OperationContext.New());
        
        await SessionService.EndSessionAsync(session, OperationContext.New());
        await SessionService.EndSessionAsync(session, OperationContext.New());
    }

    [Test, Order(2)]
    public async Task RegisterUser_FailOnPending_CleansAfter()
    {
        var userName = "t2" + Guid.NewGuid().ToString("N");
        var context = OperationContext.New();
        context.FailOnSignal("pending-account", CreateCosmoException());
        Assert.ThrowsAsync<AccountException>(async () => await AccountService.RegisterAsync(new ($"{userName}@xxx.com", userName, "Display" + userName, "pass"), context));

        var user = await SessionService.LoginWithPasswordAsync(new ($"{userName}@xxx.com", "pass"), OperationContext.New());
        Assert.IsNull(user);
        
        var deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, OperationContext.New());
        Assert.That(deleted, Is.EqualTo(0));
    }

    [Test, Order(3)]
    public async Task RegisterUser_FailDuplicateEmail_CleansAfter()
    {
        var context = OperationContext.New();
        var userName = "t3" + Guid.NewGuid().ToString("N");
        await AccountService.RegisterAsync(new ($"{userName}@xxx.com", userName, "Display" + userName, "pass"), context);
            
        context = OperationContext.New();
        var error = Assert.ThrowsAsync<AccountException>(async () => await AccountService.RegisterAsync(new ($"{userName}@xxx.com", userName, "Display" + userName, "pass2"), context));
        Assert.That(error.Error, Is.EqualTo(AccountError.EmailAlreadyRegistered));
        
        var user = await SessionService.LoginWithPasswordAsync(new ($"{userName}@xxx.com", "pass2"), OperationContext.New());
        Assert.IsNull(user);
        
        var deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, OperationContext.New());
        Assert.That(deleted, Is.EqualTo(1));
    }
    
    [Test, Order(4)]
    public async Task RegisterUser_FailOnEmailLock_CleansAfter()
    {
        var context = OperationContext.New();
        var userName = "t4" + Guid.NewGuid().ToString("N");
        context.FailOnSignal("email-lock", CreateCosmoException());
        Assert.ThrowsAsync<AccountException>(async () => await AccountService.RegisterAsync(new ($"{userName}@xxx.com", userName, "Display" + userName, "pass"), context));

        var user = await SessionService.LoginWithPasswordAsync(new ($"{userName}@xxx.com", "pass"), OperationContext.New());
        Assert.IsNull(user);
        
        var deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, OperationContext.New());
        Assert.That(deleted, Is.EqualTo(1));
        
        deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, OperationContext.New());
        Assert.That(deleted, Is.EqualTo(0));
    }
    
    [Test, Order(5)]
    public async Task RegisterUser_FailDuplicateHandle_CleansAfter()
    {
        var context = OperationContext.New();
        var userName = "t5" + Guid.NewGuid().ToString("N");
        await AccountService.RegisterAsync(new ($"{userName}@xxx.com", userName, "Display" + userName, "pass"), context);
            
        context = OperationContext.New();
        var error = Assert.ThrowsAsync<AccountException>(async () => await AccountService.RegisterAsync(new ($"{userName}@xxx2.com", userName, "Display" + userName, "pass2"), context));
        Assert.That(error.Error, Is.EqualTo(AccountError.HandleAlreadyRegistered));
        
        var user = await SessionService.LoginWithPasswordAsync(new ($"{userName}@xxx2.com", "pass"), OperationContext.New());
        Assert.IsNull(user);
        
        var deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, OperationContext.New());
        Assert.That(deleted, Is.EqualTo(1));
    }

    [Test, Order(6)]
    public async Task RegisterUser_FailOnHandleLock_CleansAfter()
    {
        var context = OperationContext.New();
        var userName = "t6" + Guid.NewGuid().ToString("N");
        context.FailOnSignal("handle-lock", CreateCosmoException());
        Assert.ThrowsAsync<AccountException>(async () => await AccountService.RegisterAsync(new ($"{userName}@xxx.com", userName, "Display" + userName, "pass"), context));

        var user = await SessionService.LoginWithPasswordAsync(new ($"{userName}@xxx.com", "pass"), OperationContext.New());
        Assert.IsNull(user);
        
        var deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, OperationContext.New());
        Assert.That(deleted, Is.EqualTo(1));
    }
    
    [Test, Order(7)]
    public async Task RegisterUser_FailOnRest_CleansAfter()
    {
        string[] signals = ["create-handle", "create-login", "create-profile"];

        foreach (var signal in signals)
        {
            var context = OperationContext.New();
            var userName = "t7" + Guid.NewGuid().ToString("N");
            context.FailOnSignal(signal, CreateCosmoException());
            Assert.ThrowsAsync<AccountException>(async () => await AccountService.RegisterAsync(new ($"{userName}@xxx.com", userName, "Display" + userName, "pass"), context));

            var user = await SessionService.LoginWithPasswordAsync(new ($"{userName}@xxx.com", "pass"), OperationContext.New());
            Assert.IsNull(user);
        
            var deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, OperationContext.New());
            Assert.That(deleted, Is.EqualTo(1));
        }
    }
    
    [Test, Order(8)]
    public async Task RegisterUser_FailOnCleanPending_AllowContinue()
    {
        var context = OperationContext.New();
        var userName = "t8" + Guid.NewGuid().ToString("N");
        context.FailOnSignal("complete-pending-account", CreateCosmoException());
        var accountId = await AccountService.RegisterAsync(new ($"{userName}@xxx.com", userName, "Display" + userName, "pass"), context);
        Assert.That(accountId, Is.Not.Null);
        
        var user = await SessionService.LoginWithPasswordAsync(new ($"{userName}@xxx.com", "pass"), OperationContext.New());
        Assert.IsNotNull(user);
        
        var deleted = await AccountService.RemovedExpiredPendingAccountsAsync(TimeSpan.Zero, OperationContext.New());
        Assert.That(deleted, Is.EqualTo(0));
    }
}