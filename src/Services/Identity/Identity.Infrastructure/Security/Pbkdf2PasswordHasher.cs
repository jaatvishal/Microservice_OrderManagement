using Identity.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace Identity.Infrastructure.Security;

/// <summary>
/// ASP.NET Core Identity's PasswordHasher (PBKDF2) is used on purpose.
/// Password hashing is cryptographic code; this service should not invent its own.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();
    private readonly object _dummyUser = new();
    private readonly string _dummyHash;

    public Pbkdf2PasswordHasher()
    {
        _dummyHash = _hasher.HashPassword(_dummyUser, "dummy-timing-equalization-value");
    }

    public string Hash(string password) => _hasher.HashPassword(_dummyUser, password);

    public PasswordVerification Verify(string password, string? passwordHash)
    {
        var hash = string.IsNullOrWhiteSpace(passwordHash) ? _dummyHash : passwordHash;
        var result = _hasher.VerifyHashedPassword(_dummyUser, hash, password);

        if (string.IsNullOrWhiteSpace(passwordHash))
            return PasswordVerification.Failed;

        return result switch
        {
            PasswordVerificationResult.Success => PasswordVerification.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordVerification.SuccessRehashNeeded,
            _ => PasswordVerification.Failed
        };
    }
}
