using Identity.Application.Models;
using Identity.Domain.Users;

namespace Identity.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByNormalizedEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<User?> GetByRefreshTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken);

    Task<User?> GetByIdForUpdateAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Read model for the caller's profile. The query must not select the password hash.
    /// </summary>
    Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(User user, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task<PagedList<UserSummary>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
}
