using Identity.Domain.Users;

namespace Identity.Application.Abstractions;

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}

public sealed record AccessToken(string Value, DateTime ExpiresAtUtc);
