using BuildingBlocks.Application.Common;
using BuildingBlocks.Application.Models;
using FluentValidation;
using Identity.Application.Abstractions;
using Identity.Application.Models;
using Identity.Domain.Users;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Auth.Register;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? IpAddress);

public sealed class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(256)
            .Must(email => email is not null && !email.Contains('\r') && !email.Contains('\n'))
            .WithMessage("Email must not contain line breaks.")
            .EmailAddress();

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");

        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
    }
}

public sealed class RegisterUserHandler(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IRefreshTokenService refreshTokens,
    IAccessTokenIssuer accessTokens,
    IDateTimeProvider clock,
    IValidator<RegisterUserCommand> validator,
    ILogger<RegisterUserHandler> logger)
{
    public async Task<Result<AuthenticationResult>> Handle(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            var message = string.Join(' ', validation.Errors.Select(error => error.ErrorMessage));
            return Result.Failure<AuthenticationResult>(IdentityErrors.Validation(message));
        }

        var normalizedEmail = command.Email.Trim().ToUpperInvariant();
        var existing = await users.GetByNormalizedEmailForUpdateAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation("Registration rejected because the email is already in use. Address {MaskedEmail}", EmailAddressMask.Apply(command.Email));
            return Result.Failure<AuthenticationResult>(IdentityErrors.EmailConflict());
        }

        // Public registration cannot choose a role. Admin accounts are seeded out of band.
        var user = User.Register(
            command.Email,
            passwordHasher.Hash(command.Password),
            command.FirstName,
            command.LastName,
            clock.UtcNow);

        var authentication = AuthenticationSession.Issue(user, command.IpAddress, clock, refreshTokens, accessTokens);
        await users.AddAsync(user, cancellationToken);

        try
        {
            await users.SaveChangesAsync(cancellationToken);
        }
        catch (EmailAlreadyRegisteredException)
        {
            logger.LogInformation(
                "Registration lost a uniqueness race for {MaskedEmail}",
                EmailAddressMask.Apply(command.Email));
            return Result.Failure<AuthenticationResult>(IdentityErrors.EmailConflict());
        }

        logger.LogInformation("Registered user {UserId}", user.Id);
        return Result.Success(authentication);
    }
}
