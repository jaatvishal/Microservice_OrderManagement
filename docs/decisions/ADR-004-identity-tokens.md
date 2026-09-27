# ADR-004: Identity Tokens and Password Hashing

## Status
Accepted

## Context
The platform needs a single authority that proves who a caller is. Later services (Product, Cart, Order, Inventory, Payment) must authorize requests without reading the Identity database.

Three designs were considered:

1. A shared session table that every service queries.
2. ASP.NET Core Identity end to end (`UserManager`, `IdentityDbContext`, its token table).
3. A small Identity service that issues signed access tokens and stores only a hash of each refresh token.

A shared session table recreates the shared-database coupling rejected in ADR-001. Full ASP.NET Identity brings tables and behaviors this domain does not need (external logins, authenticator keys, a generic token blob) and does not model refresh-token reuse detection.

## Decision
Identity is the only component that writes `IdentityDb`.

- Passwords are hashed with ASP.NET Core Identity's PBKDF2 `PasswordHasher`. This service does not implement its own password KDF.
- Access tokens are JWTs signed with HMAC-SHA256. Lifetime default is 15 minutes. They are not stored.
- Refresh tokens are 256 bits of randomness. Identity stores SHA-256 of the token, not the raw value. Each successful refresh rotates the token.
- Presenting a refresh token that was already rotated, outside a short grace window, revokes every active session for that user.
- Role claims are `Customer` and `Admin`. Public registration always creates `Customer`. There is no request field that can set the role.
- The signing key is required at startup and must be at least 32 bytes. It is read from configuration (`Jwt__SigningKey` via environment, user-secrets, or Key Vault). It is not committed.
- Other services will validate the JWT locally. They will not call Identity on each request and they will not open `IdentityDb`.

Production should move the signing key to an asymmetric key (RS256 or ES256) whose private key stays in Key Vault and whose public key is exposed through JWKS. HMAC is acceptable while only Identity signs tokens and every validator must hold the same secret. The configuration shape (`Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey`) does not have to change for the Azure host; the secret source does.

## Consequences

### Positive
- A leaked `IdentityDb` backup does not reveal passwords or live refresh tokens.
- Order, Inventory, and Payment can authorize with the token alone. Identity can be down and already-issued access tokens still work until they expire.
- Session theft has a detectable signal: reuse of a rotated refresh token.

### Negative
- Access tokens cannot be revoked instantly. Deactivation and logout take effect at the next refresh, or within the access-token lifetime. That is why the access token is short.
- Every service that validates HMAC tokens must hold the signing key. That blast radius is the reason to move to asymmetric keys before production.
- A lost refresh response forces the client to sign in again after the grace window. The successor token cannot be replayed because only its hash was stored. A later phase can cache the issued pair for a few seconds if that support burden shows up.
