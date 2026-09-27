namespace Identity.Application.Auth;

internal static class EmailAddressMask
{
    public static string Apply(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return "***";

        var at = email.IndexOf('@');
        if (at <= 1)
            return "***";

        return $"{email[0]}***{email[at..]}";
    }
}
