namespace BuildingBlocks.Contracts.Identity;

/// <summary>
/// Role claim values issued by Identity and enforced by every other service.
/// These strings are a platform contract. Renaming one invalidates existing tokens.
/// </summary>
public static class PlatformRoles
{
    public const string Customer = "Customer";

    public const string Admin = "Admin";
}
