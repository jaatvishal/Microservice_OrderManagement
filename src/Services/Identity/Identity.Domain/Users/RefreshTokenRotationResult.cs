namespace Identity.Domain.Users;

public enum RefreshTokenRotationStatus
{
    Success = 1,
    Invalid = 2,
    ReuseDetected = 3,
    RotatedWithinGrace = 4
}

public sealed record RefreshTokenRotationResult(RefreshTokenRotationStatus Status, RefreshToken? Token)
{
    public static RefreshTokenRotationResult Succeeded(RefreshToken token) =>
        new(RefreshTokenRotationStatus.Success, token);

    public static RefreshTokenRotationResult Invalid() =>
        new(RefreshTokenRotationStatus.Invalid, null);

    public static RefreshTokenRotationResult ReuseDetected() =>
        new(RefreshTokenRotationStatus.ReuseDetected, null);

    public static RefreshTokenRotationResult RotatedWithinGrace() =>
        new(RefreshTokenRotationStatus.RotatedWithinGrace, null);
}
