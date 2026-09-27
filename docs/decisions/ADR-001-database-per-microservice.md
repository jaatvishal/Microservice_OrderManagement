# ADR-001: Database per Microservice

## Status
Accepted

## Context
In a monolithic application, all business domains typically share a single relational database schema. Multiple modules execute cross-table joins, share transactions, and rely on foreign key constraints enforced by the database engine.

When transitioning to a distributed microservices architecture for an E-Commerce platform handling hundreds of thousands of concurrent checkouts, sharing a single centralized database introduces fatal operational and architectural liabilities:
1. **Tight Coupling of Schema Changes:** A schema change to the Product or Inventory schema can inadvertently break the Order or Cart services.
2. **Blast Radius & Single Point of Failure:** If a bad query in reporting or analytics locks tables or consumes 100% database CPU, the entire platform (Identity, Ordering, Payment) goes down.
3. **Database Contention & Lock Escalation:** High-concurrency operations (e.g. stock decrementing) hold row or table locks, creating severe cascading latency across unrelated customer services.
4. **Polyglot Persistence Impossibility:** Different workloads require different storage engines. Cart state thrives on low-latency, memory-backed distributed key-value stores (Redis with auto-expiring keys), whereas financial transactions require ACID relational databases (SQL Server/Azure SQL).

## Decision
We enforce a strict **Database-per-Microservice** architectural pattern:
- Each microservice owns its private persistent datastore.
- Microservices **must not** directly query, read, or write to another service's database under any circumstances.
- No direct database connection strings across boundaries are permitted.
- All inter-service data access occurs through public REST APIs (synchronous queries) or integration events (asynchronous data synchronization).
- Database engines are chosen according to the domain workload:
  - `IdentityDb` (SQL Server): Relational user, credentials, and refresh token tables.
  - `ProductDb` (SQL Server + Redis Read Cache): Product catalog, categories, pricing.
  - `CartStore` (Redis): Transient basket state with key expiration.
  - `OrderDb` (SQL Server): Order aggregate roots, line items, and Transactional Outbox table.
  - `InventoryDb` (SQL Server): SKU stock levels, reservations, and Outbox table.
  - `PaymentDb` (SQL Server): Payment records, idempotency keys, transaction audit logs.
  - `NotificationDb` (SQL Server / Document Store): Notification delivery audit logs and templates.

## Consequences

### Positive
- **Independent Deployability:** A microservice team can alter table structures, add migrations, or refactor schemas without testing or coordinating with other service teams.
- **Fault Isolation:** An outage or severe degradation of `InventoryDb` does not crash `ProductDb` or `IdentityDb`.
- **Targeted Scaling:** Heavy write services like `InventoryDb` and `OrderDb` can be scaled up independently or partitioned, while read-heavy services use caching replicas.
- **Security Boundaries:** Payment credit card tokens or customer password hashes in `IdentityDb` are isolated behind least-privilege network access rules and cannot be reached by public-facing catalog queries.

### Negative & Trade-offs
- **No Distributed ACID Transactions:** Two-phase commit (2PC) is prohibited due to blocking and latency. Cross-service transactional consistency must be handled using the Saga Pattern and Eventual Consistency.
- **No Cross-Database SQL Joins:** To display order details with rich product descriptions, the Order Service must either store a snapshot of product metadata at order placement time or query the Product Service API via Gateway aggregation.
- **Operational Complexity:** Managing backup, recovery, connection pooling, and schema migrations across multiple independent databases requires standardized CI/CD pipelines and IaC (Terraform / Bicep).
