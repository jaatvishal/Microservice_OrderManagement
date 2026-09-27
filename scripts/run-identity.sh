#!/usr/bin/env bash
set -euo pipefail

if [[ -z "${ConnectionStrings__IdentityDb:-}" || -z "${Jwt__SigningKey:-}" ]]; then
  echo "Set ConnectionStrings__IdentityDb and Jwt__SigningKey before starting Identity."
  echo "See .env.example. Do not put real values in source control."
  exit 1
fi

export Database__ApplyMigrations="${Database__ApplyMigrations:-true}"
dotnet run --project src/Services/Identity/Identity.API
