$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__IdentityDb) -or [string]::IsNullOrWhiteSpace($env:Jwt__SigningKey)) {
    Write-Error "Set ConnectionStrings__IdentityDb and Jwt__SigningKey before starting Identity. See .env.example. Do not put real values in source control."
}

if ([string]::IsNullOrWhiteSpace($env:Database__ApplyMigrations)) {
    $env:Database__ApplyMigrations = "true"
}

dotnet run --project src/Services/Identity/Identity.API
