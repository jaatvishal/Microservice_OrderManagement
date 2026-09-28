$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__ProductDb) -or [string]::IsNullOrWhiteSpace($env:Jwt__SigningKey)) {
    Write-Error "Set ConnectionStrings__ProductDb and Jwt__SigningKey before starting Product. The signing key must match Identity. See .env.example."
}

if ([string]::IsNullOrWhiteSpace($env:Database__ApplyMigrations)) {
    $env:Database__ApplyMigrations = "true"
}

if ([string]::IsNullOrWhiteSpace($env:Database__SeedSampleCatalog)) {
    $env:Database__SeedSampleCatalog = "true"
}

dotnet run --project src/Services/Product/Product.API
