using Identity.Application.Abstractions;
using Identity.Application.Auth;
using Identity.Application.Models;
using Identity.Domain.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed class UserRepository(IdentityDbContext context) : IUserRepository
{
    public Task<User?> GetByNormalizedEmailForUpdateAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        context.Users.SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<User?> GetByRefreshTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.Users
            .Include(user => user.RefreshTokens)
            .SingleOrDefaultAsync(
                user => user.RefreshTokens.Any(token => token.TokenHash == tokenHash),
                cancellationToken);

    public Task<User?> GetByIdForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Users
            .Include(user => user.RefreshTokens)
            .SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<UserProfile?> GetProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new UserProfile(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Role == UserRole.Admin ? "Admin" : "Customer",
                user.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken) =>
        await context.Users.AddAsync(user, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new IdentityConcurrencyException(exception);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new EmailAlreadyRegisteredException(exception);
        }
    }

    public async Task<PagedList<UserSummary>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        // Projection skips the password hash and refresh tokens. AsNoTracking skips the
        // change-tracker cost for a read that will never be saved.
        var query = context.Users.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(user => user.CreatedAtUtc)
            .ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new UserSummary(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.Role == UserRole.Admin ? "Admin" : "Customer",
                user.IsActive,
                user.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedList<UserSummary>(items, page, pageSize, total);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };
}
