namespace Identity.Application.Auth;

public sealed class EmailAlreadyRegisteredException : Exception
{
    public EmailAlreadyRegisteredException()
        : base("An account with this email already exists.")
    {
    }

    public EmailAlreadyRegisteredException(Exception innerException)
        : base("An account with this email already exists.", innerException)
    {
    }
}

public sealed class IdentityConcurrencyException : Exception
{
    public IdentityConcurrencyException(Exception innerException)
        : base("The account was updated by another request.", innerException)
    {
    }
}
