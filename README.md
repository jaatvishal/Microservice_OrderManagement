# E-Commerce Order Management Platform

An enterprise-grade, production-oriented .NET 8 Microservices reference architecture demonstrating real-world distributed systems patterns, high-scale cloud engineering, reliability, security, and observability.

---

## 🏛️ Architecture Overview

The system is designed as an autonomous, event-driven distributed platform following **Clean Architecture**, **Domain-Driven Design (DDD)** concepts, **Database-per-Microservice**, and **Choreographed Saga with Transactional Outbox**.

```
[ Web / Mobile Clients ]
           │
           ▼
[ API Gateway (YARP Reverse Proxy) ]
  ├── Routing & Rate Limiting
  ├── Centralized JWT Validation
  └── Request Correlation Tracking (X-Correlation-ID)
           │
 ┌─────────┼───────────────┬────────────────┬───────────────┐
 ▼         ▼               ▼                ▼               ▼
Identity  Product        Cart             Order          Inventory
Service   Service        Service          Service        Service
   │         │              │                │              │
[IdentityDb] [ProductDb] [Redis Cart]    [OrderDb]     [InventoryDb]
                            │                │              │
                            │      ┌─────────┴──────────────┴─────────┐
                            │      │ Asynchronous Event Bus (ASB)     │
                            │      │ Integration Events:              │
                            │      │ - OrderCreated                   │
                            │      │ - InventoryReserved / Failed     │
                            │      │ - PaymentCompleted / Failed      │
                            │      │ - OrderConfirmed / Cancelled     │
                            │      └─────────┬──────────────┬─────────┘
                            │                ▼              ▼
                            │             Payment       Notification
                            │             Service       Service
                            │                │              │
                            │           [PaymentDb]   [NotificationDb]
```

Detailed diagrams and technical blueprints are documented in [`docs/architecture/system-architecture.md`](docs/architecture/system-architecture.md).

---

## 📦 Microservices & Responsibilities

| Service | Primary Responsibility | Data Store | Key Communication Pattern |
| :--- | :--- | :--- | :--- |
| **API Gateway** | Single entry point, SSL termination, routing, rate limiting, correlation ID injection | None | Synchronous reverse proxy (YARP) |
| **Identity Service** | User registration, authentication, JWT tokens, refresh tokens, role claims | `IdentityDb` (SQL) | Synchronous REST |
| **Product Service** | Product catalog management, category filtering, search, pricing | `ProductDb` (SQL + Redis) | Synchronous REST (Cache-Aside) |
| **Cart Service** | Active customer shopping cart, item addition, basket lifecycle | `Redis CartStore` | Synchronous REST + Redis TTL |
| **Order Service** | Order creation, state management, checkout orchestration | `OrderDb` (SQL + Outbox) | Sync REST (Accept) + Async Events |
| **Inventory Service** | Stock tracking, allocation holds, reservations, restocking | `InventoryDb` (SQL + Outbox) | Asynchronous Event Consumer & Publisher |
| **Payment Service** | Payment gateway integration, idempotency check, transaction log | `PaymentDb` (SQL + Outbox) | Asynchronous Event Consumer & Publisher |
| **Notification Service** | Customer transactional emails, SMS, push alerts | `NotificationDb` (SQL) | Asynchronous Event Consumer |

---

## 🧱 Solution Structure

```
ECommerce.OrderManagement/
├── src/
│   ├── BuildingBlocks/
│   │   ├── BuildingBlocks.Contracts/       # Event contracts and DTOs (Zero-dependency)
│   │   ├── BuildingBlocks.Application/     # Result monad, common abstractions (Clock, Tracing)
│   │   └── BuildingBlocks.Infrastructure/  # Concrete providers (DateTimeProvider, CorrelationContext)
│   ├── Services/
│   │   ├── Identity/                       # Phase 2: accounts, JWT, refresh tokens, IdentityDb
│   │   ├── Product/
│   │   ├── Cart/
│   │   ├── Order/
│   │   ├── Inventory/
│   │   ├── Payment/
│   │   └── Notification/
│   └── Gateway/
│       └── ApiGateway/
├── tests/
│   ├── UnitTests/                          # Fast isolated domain & application logic tests
│   ├── IntegrationTests/                   # End-to-end API & Testcontainers DB tests
│   └── ContractTests/                      # Consumer-Driven Contract testing
├── docs/
│   ├── architecture/                       # System architecture specs and Mermaid diagrams
│   └── decisions/                          # Architecture Decision Records (ADRs)
├── deploy/
│   ├── docker/                             # Dockerfiles & local docker-compose
│   └── azure/                              # Bicep / Terraform cloud infrastructure
├── Directory.Build.props                   # Solution-wide C# 12 / .NET 8 compiler settings
└── README.md
```

---

## Getting started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server or Azure SQL for the Identity database (local Docker Compose arrives in a later phase)
- Git

### Build and test
```bash
dotnet build
dotnet test
```

### Run the Identity service
Secrets are not in source control. Set them in the shell, then start the API. See [`.env.example`](.env.example).

```powershell
$env:ConnectionStrings__IdentityDb = "Server=localhost,1433;Database=IdentityDb;User Id=sa;Password=<strong-password>;TrustServerCertificate=True"
$env:Jwt__SigningKey = "<random value of at least 32 characters>"
$env:Database__ApplyMigrations = "true"
dotnet run --project src/Services/Identity/Identity.API
```

Swagger: `http://localhost:5101/swagger`

Live health (does not touch the database): `http://localhost:5101/health/live`  
Readiness (checks IdentityDb): `http://localhost:5101/health/ready`

The design of registration, tokens, and `IdentityDb` is in [`docs/architecture/identity-service.md`](docs/architecture/identity-service.md).

---

## Architecture Decision Records
- [ADR-001: Database per Microservice](docs/decisions/ADR-001-database-per-microservice.md)
- [ADR-002: Synchronous REST vs Asynchronous Messaging](docs/decisions/ADR-002-sync-rest-vs-async-messaging.md)
- [ADR-003: Shared BuildingBlocks Discipline vs Distributed Coupling](docs/decisions/ADR-003-building-blocks-discipline.md)
- [ADR-004: Identity tokens and password hashing](docs/decisions/ADR-004-identity-tokens.md)
