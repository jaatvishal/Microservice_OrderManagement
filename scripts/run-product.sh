#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${ConnectionStrings__ProductDb:-}" || -z "${Jwt__SigningKey:-}" ]]; then
  echo "Set ConnectionStrings__ProductDb and Jwt__SigningKey before starting Product."
  echo "The signing key must be the same value Identity uses. See .env.example."
  exit 1
fi

export Database__ApplyMigrations="${Database__ApplyMigrations:-true}"
export Database__SeedSampleCatalog="${Database__SeedSampleCatalog:-true}"
dotnet run --project src/Services/Product/Product.API
