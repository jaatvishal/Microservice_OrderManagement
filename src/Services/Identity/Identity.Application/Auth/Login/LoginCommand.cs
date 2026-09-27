using BuildingBlocks.Application.Common;
using BuildingBlocks.Application.Models;
using FluentValidation;
using Identity.Application.Abstractions;
using Identity.Application.Models;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Auth.Login;

public sealed record LoginCommand(string Email, string Password, string? IpAddress);

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(256);
        // Complexity rules are not repeated here. A login request must not tell an
        // attacker which password policy the account was created under.
        RuleFor(command => command.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class LoginHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IRefreshTokenService refreshTokens,
    IAccessTokenIssuer accessTokens,
    IDateTimeProvider clock,
    IValidator<LoginCommand> validator,
    ILogger<LoginHandler> logger)
{
    public async Task<Result<AuthenticationResult>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            var message = string.Join(' ', validation.Errors.Select(error => error.ErrorMessage));
            return Result.Failure<AuthenticationResult>(IdentityErrors.Validation(message));
        }

        var normalizedEmail = command.Email.Trim().ToUpperInvariant();
        var user = await users.GetByNormalizedEmailForUpdateAsync(normalizedEmail, cancellationToken);
        var verification = passwordHasher.Verify(command.Password, user?.PasswordHash);

        if (user is null || !user.IsActive || verification == PasswordVerification.Failed)
        {
            logger.LogInformation("Failed login for {MaskedEmail}", EmailAddressMask.Apply(command.Email));
            return Result.Failure<AuthenticationResult>(IdentityErrors.InvalidCredentials);
        }

        if (verification == PasswordVerification.SuccessRehashNeeded)
            user.ReplacePasswordHash(passwordHasher.Hash(command.Password));

        var authentication = AuthenticationSession.Issue(user, command.IpAddress, clock, refreshTokens, accessTokens);
        await users.SaveChangesAsync(cancellationToken);

        logger.LogInformation("User {UserId} signed in", user.Id);
        return Result.Success(authentication);
    }
}
