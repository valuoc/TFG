namespace SocialApp.WebApi.Features.Session.Models;

public record UserSession(string UserId, string SessionId, string DisplayName, string Handle, bool HasPendingOperations);