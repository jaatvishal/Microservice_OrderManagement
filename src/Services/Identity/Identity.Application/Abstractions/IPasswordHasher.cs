namespace Identity.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);

    /// <summary>
    /// Pass a null hash when the user does not exist so the implementation still
    /// performs a verification against a dummy hash. That keeps unknown-user and
    /// wrong-password responses on a similar timeline.
    /// </summary>
    PasswordVerification Verify(string password, string? passwordHash);
}

public enum PasswordVerification
{
    Failed = 0,
    Success = 1,
    SuccessRehashNeeded = 2
}
