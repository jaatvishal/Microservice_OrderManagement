# ADR-003: Shared BuildingBlocks Discipline vs Distributed Coupling

## Status
Accepted

## Context
When engineering microservice systems in .NET, a common anti-pattern is creating a massive shared class library (e.g., `Common.dll` or `SharedKernel.dll`) containing domain entities, EF Core DbContexts, business rules, and utility classes. 

This creates **Distributed Monolith** pathology:
- Any change to the shared library requires re-testing, rebuilding, and redeploying all microservices simultaneously.
- Microservices inadvertently share domain models, destroying domain encapsulation and autonomy.
- Breaking API or domain model changes propagate uncontrollably.

However, completely duplicating boilerplate code (e.g., base `IntegrationEvent` records, `Result<T>` error monads, correlation accessor interfaces) across 7 separate microservice repositories can lead to unnecessary drift and bugs.

## Decision
We enforce a strict three-tier modular `BuildingBlocks` structure with rigorous boundaries:

1. **`BuildingBlocks.Contracts` (Zero Dependencies):**
   - Contains only public integration event interfaces, record contracts, and primitive DTOs that cross service boundaries via the message broker.
   - Contains **no** business logic, **no** database logic, and **no** framework dependencies.
   - Designed for stability and backward compatibility.

2. **`BuildingBlocks.Application` (Core Abstractions):**
   - Standard abstractions for error handling (`Result`, `Result<T>`, `Error`).
   - Standard interfaces for cross-cutting application capabilities: `IDateTimeProvider`, `ICorrelationContextAccessor`, `IEventBus`.
   - Never contains domain models or business rules.

3. **`BuildingBlocks.Infrastructure` (Common Reusable Plumbing):**
   - Concrete implementations of core abstractions: `DateTimeProvider`, `CorrelationContextAccessor` (backed by `AsyncLocal<T>`).
   - Cross-cutting infrastructural concerns (e.g., Serilog enrichment, health check extensions, OpenTelemetry helpers) added in later phases.

### Golden Rules
1. **No Domain Entities in BuildingBlocks:** Domain entities belong strictly inside `Services/{Service}/Domain`.
2. **No Business Rules in BuildingBlocks:** Each microservice implements and protects its own domain invariants.
3. **Contracts are Immutable and Append-Only:** New fields in integration events must be optional/nullable to guarantee backward compatibility for running consumers.

## Consequences

### Positive
- Prevents code duplication for non-differentiating technical infrastructure (tracing, error types, event interfaces).
- Protects domain boundaries and team autonomy.
- Avoids circular dependencies and the distributed monolith trap.

### Negative & Trade-offs
- Developers must maintain discipline and resist the temptation to drop domain models or helpers into BuildingBlocks.
