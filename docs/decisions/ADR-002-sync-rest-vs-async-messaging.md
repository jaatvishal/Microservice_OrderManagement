# ADR-002: Synchronous REST vs Asynchronous Messaging

## Status
Accepted

## Context
Microservices need to interact with each other to fulfill business requirements. In distributed systems, communication patterns broadly fall into two categories:
1. **Synchronous Request/Response** (HTTP/REST, gRPC)
2. **Asynchronous Event-Driven Messaging** (Message Broker queues/topics: Azure Service Bus, RabbitMQ)

Using synchronous HTTP calls for multi-service business workflows creates severe problems in production:
- **Temporal Coupling:** If Service A calls Service B, which calls Service C, all three services must be online, reachable, and healthy at that exact millisecond. If Service C is slow or down, Service A fails or exhausts thread pool resources.
- **Cascading Latency:** System response time is the sum of all serial HTTP requests ($T_{total} = T_A + T_B + T_C + \dots$), drastically degrading user experience.
- **Distributed Failure Vulnerability:** If the customer pays and the network blinks between Order Service and Notification Service, the user may see a 500 error even though their credit card was charged.

## Decision
We adopt a hybrid communication strategy governed by strict architectural guidelines:

1. **Synchronous REST via API Gateway (Client-to-Service and Queries):**
   - Ingress from Web/Mobile through YARP API Gateway into individual microservices.
   - Read queries that require immediate real-time answers (e.g., `GET /api/products/123`, `POST /api/auth/login`, `GET /api/cart`).
   - Short, non-cascading queries where failure can be communicated immediately to the caller.

2. **Asynchronous Event-Driven Messaging (Inter-Service Business Workflows):**
   - High-value transactional workflows spanning multiple services (such as Order Placement, Stock Reservation, Payment Authorization, and Notification Dispatch).
   - Microservices publish immutable **Integration Events** using the **Transactional Outbox Pattern**.
   - Azure Service Bus Topics / Subscriptions in production (and RabbitMQ for isolated local development).
   - At-least-once delivery semantics coupled with mandatory idempotent consumer processing.

3. **No Direct Inter-Service Cascading HTTP Chains:**
   - The Order Service **must not** make synchronous HTTP calls to Inventory Service, Payment Service, and Notification Service in a single user request.
   - Instead, the Order Service creates the order in a `Pending` state, commits an `OrderCreatedIntegrationEvent` to its Outbox, and returns `202 Accepted` to the client. The downstream processing continues asynchronously.

## Consequences

### Positive
- **High Availability & Decoupling:** Even if the Notification Service is temporarily restarting or degraded during deployment, orders are still accepted, paid, and confirmed. Emails are processed as soon as the service recovers.
- **Traffic Leveling (Queue Buffering):** Flash sales or Black Friday spikes are absorbed by message broker topics, allowing downstream inventory and payment workers to process jobs at their optimal throughput without crashing.
- **Auditability & Observability:** Every event published contains `CorrelationId`, `CausationId`, and timestamp, providing an immutable audit trail of domain events.

### Negative & Trade-offs
- **Eventual Consistency:** The user experiences a brief period where their order is `Pending` before reaching `Confirmed`. The UI must accommodate this pattern (e.g. polling or WebSockets / Server-Sent Events).
- **Complexity of Message Contracts:** Event schemas must be backward compatible; changes require disciplined versioning.
- **Operational Overhead:** Requires monitoring message broker queue depths, dead-letter queues (DLQ), consumer lag, and retries.
