# Overall System Architecture

This document outlines the architectural blueprint for the **E-Commerce Order Management Platform**, designed from the ground up for high reliability, autonomous scalability, clean boundaries, and zero-downtime evolution.

---

## 1. High-Level Topology

```mermaid
graph TB
    subgraph Clients["Clients & Consumers"]
        Web[Web / Single Page Application]
        Mobile[Mobile Application]
        AdminApp[Backoffice Admin Portal]
    end

    subgraph Edge["Edge & Entry"]
        Gateway["API Gateway (YARP)<br/>- Routing & Reverse Proxy<br/>- JWT Token Validation<br/>- Rate Limiting & SSL Termination<br/>- Request Correlation Tagging"]
    end

    subgraph Services["Autonomous Microservices (Domain Boundaries)"]
        Identity["Identity Service<br/>(Auth, JWT, Refresh Tokens)"]
        Product["Product Service<br/>(Catalog, Search, Pricing, Redis)"]
        Cart["Cart Service<br/>(Customer Basket, Redis Active State)"]
        Order["Order Service<br/>(Order Lifecycle, Saga Coordinator)"]
        Inventory["Inventory Service<br/>(SKU Stock, Reservations, Holds)"]
        Payment["Payment Service<br/>(Gateway Adapters, Idempotent Processing)"]
        Notification["Notification Service<br/>(Transactional Emails, Push)"]
    end

    subgraph Data["Database-per-Microservice (Strict Isolation)"]
        IdentityDb[(IdentityDb)]
        ProductDb[(ProductDb)]
        CartDb[(Redis CartStore)]
        OrderDb[(OrderDb + Outbox)]
        InventoryDb[(InventoryDb + Outbox)]
        PaymentDb[(PaymentDb + Outbox)]
        NotificationDb[(NotificationDb)]
    end

    subgraph Messaging["Asynchronous Event Fabric"]
        Bus[Message Bus / Azure Service Bus / RabbitMQ<br/>Topics & Subscriptions with Dead-Letter Queues]
    end

    %% Client traffic
    Web --> Gateway
    Mobile --> Gateway
    AdminApp --> Gateway

    %% Synchronous Gateway routing
    Gateway -->|/api/auth| Identity
    Gateway -->|/api/products| Product
    Gateway -->|/api/cart| Cart
    Gateway -->|/api/orders| Order
    Gateway -->|/api/inventory| Inventory
    Gateway -->|/api/payments| Payment

    %% Service to Database
    Identity --- IdentityDb
    Product --- ProductDb
    Cart --- CartDb
    Order --- OrderDb
    Inventory --- InventoryDb
    Payment --- PaymentDb
    Notification --- NotificationDb

    %% Asynchronous Events
    Order -.->|Publish: OrderCreated| Bus
    Inventory -.->|Publish: InventoryReserved / Failed| Bus
    Payment -.->|Publish: PaymentCompleted / Failed| Bus
    Order -.->|Publish: OrderConfirmed / Cancelled| Bus
    Bus -.->|Consume: Stock Allocation| Inventory
    Bus -.->|Consume: Charge Authorization| Payment
    Bus -.->|Consume: Order Status Orchestration| Order
    Bus -.->|Consume: Customer Email Alerts| Notification
```

---

## 2. Order Fulfillment Flow (Choreographed Saga & Compensation)

```mermaid
sequenceDiagram
    autonumber
    actor Customer
    participant Gateway as API Gateway
    participant OrderSvc as Order Service
    participant OrderDb as OrderDb (Outbox)
    participant Bus as Message Bus (ASB)
    participant InvSvc as Inventory Service
    participant PaySvc as Payment Service
    participant NotifSvc as Notification Service

    Customer->>Gateway: POST /api/orders (Items, DeliveryAddress)
    Gateway->>OrderSvc: Forward with X-Correlation-ID
    Note over OrderSvc,OrderDb: Atomic DB Transaction:<br/>Insert Order (Status: Pending)<br/>Insert Outbox (OrderCreatedIntegrationEvent)
    OrderSvc-->>Gateway: 202 Accepted (OrderId, Status: Pending)
    Gateway-->>Customer: 202 Accepted (OrderId, Status: Pending)

    Note over OrderSvc,Bus: Outbox Worker polls & dispatches
    OrderSvc->>Bus: Publish OrderCreatedIntegrationEvent

    par Stock Reservation
        Bus->>InvSvc: Consume OrderCreatedIntegrationEvent
        Note over InvSvc: Check available stock for SKUs
        alt Stock Available
            Note over InvSvc: Decrement stock, create Reservation
            InvSvc->>Bus: Publish InventoryReservedIntegrationEvent
        else Stock Insufficient
            InvSvc->>Bus: Publish InventoryReservationFailedIntegrationEvent
        end
    end

    par Payment Processing
        Bus->>PaySvc: Consume InventoryReservedIntegrationEvent
        Note over PaySvc: Idempotent Payment processing (Stripe / Bank)
        alt Payment Succeeded
            PaySvc->>Bus: Publish PaymentCompletedIntegrationEvent
        else Payment Failed
            PaySvc->>Bus: Publish PaymentFailedIntegrationEvent
        end
    end

    par Saga Completion or Compensation
        alt Happy Path (Payment Succeeded)
            Bus->>OrderSvc: Consume PaymentCompletedIntegrationEvent
            Note over OrderSvc: Update Order Status -> Confirmed
            OrderSvc->>Bus: Publish OrderConfirmedIntegrationEvent
            Bus->>NotifSvc: Consume OrderConfirmedIntegrationEvent
            Note over NotifSvc: Send receipt email to Customer
        else Unhappy Path (Payment Failed)
            Bus->>OrderSvc: Consume PaymentFailedIntegrationEvent
            Note over OrderSvc: Update Order Status -> Cancelled
            OrderSvc->>Bus: Publish OrderCancelledIntegrationEvent
            Bus->>InvSvc: Consume OrderCancelledIntegrationEvent
            Note over InvSvc: Compensation: Release reserved stock
            Bus->>NotifSvc: Consume OrderCancelledIntegrationEvent
            Note over NotifSvc: Send payment failed notification
        end
    end
```

---

## 3. Transactional Outbox Pattern

```mermaid
flowchart LR
    subgraph ServiceBoundary["Service Boundary (e.g. Order Service)"]
        Cmd[Process Command] --> Trans["Single SQL Transaction"]
        subgraph DB["Relational Database"]
            Trans -->|1. Commit Entity| BizTable[Order Table]
            Trans -->|2. Commit Event| OutboxTable[Outbox Table]
        end
        Worker["Outbox Background Worker"] -->|Poll / Change-Data-Capture| OutboxTable
    end

    Worker -->|3. Reliable At-Least-Once Publish| Broker["Message Bus (Azure Service Bus)"]
    Broker -->|4. Acknowledge Receipt| Worker
    Worker -->|5. Mark Dispatched / Delete| OutboxTable
```

---

## 4. Azure Production Deployment Blueprint

```mermaid
graph TB
    Internet((Public Internet)) --> AFD[Azure Front Door / Cloudflare WAF]
    AFD --> APIM[Azure API Management / Ingress Controller]
    
    subgraph ACA["Azure Container Apps / AKS (Managed Kubernetes)"]
        GwyApp[api-gateway pod]
        IdApp[identity-service pod]
        ProdApp[product-service pod]
        CartApp[cart-service pod]
        OrdApp[order-service pod]
        InvApp[inventory-service pod]
        PayApp[payment-service pod]
        NotifApp[notification-service pod]
    end

    APIM --> GwyApp
    GwyApp --> IdApp & ProdApp & CartApp & OrdApp & InvApp & PayApp & NotifApp

    subgraph AzurePaaS["Azure Managed Platform Services"]
        ASQL[(Azure SQL Database - Elastic Pool)]
        ARedis[(Azure Cache for Redis)]
        ASB[(Azure Service Bus Premium)]
        AKV[Azure Key Vault - Managed Identities]
        AppIns[Azure Application Insights / OpenTelemetry]
    end

    IdApp & ProdApp & OrdApp & InvApp & PayApp & NotifApp -.->|Managed Identity Auth| AKV
    IdApp & ProdApp & OrdApp & InvApp & PayApp & NotifApp -.->|Telemetry & Tracing| AppIns
    CartApp & ProdApp --- ARedis
    OrdApp & InvApp & PayApp & NotifApp --- ASB
    IdApp & ProdApp & OrdApp & InvApp & PayApp --- ASQL
```
