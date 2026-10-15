# Architecture

## Modules and dependency direction

| Project | Responsibility | References |
|---|---|---|
| Commerce.Domain | Immutable products, users, orders, line snapshots | None |
| Commerce.Application | Use cases, repository/payment/password/lock abstractions | Domain |
| Commerce.Infrastructure | Thread-safe in-memory repositories, PBKDF2, Stripe and simulator adapters | Application, Domain |
| Commerce.Api | Controllers, HTTP contracts, cookies/CSRF, authorization, DI | Application, Infrastructure |
| Commerce.Checks | Executable regression checks with no test framework dependency | Infrastructure |

API → Application → Domain. Infrastructure implements Application interfaces. API references Infrastructure only for the composition root. This is a modular monolith; no unnecessary microservices, mediator framework or generic repository layer.

## Patterns

- Repository: focused product, user, order interfaces. A database does not leak into controllers.
- Dependency injection/inversion: .NET container connects interfaces to implementations. Repositories and mutation lock are singletons so the dummy store survives multiple HTTP requests. Services are scoped; Stripe uses HttpClientFactory.
- Adapter: IPaymentGateway isolates provider transport and callback interpretation.
- SOLID: controllers handle HTTP; application workflows handle rules; storage adapters handle data. Payment providers can be replaced through one abstraction.
- Immutable snapshots: order lines keep product names/prices at purchase time. Product updates do not change past orders.

The demo uses one application service for the bounded MVP, organized into use-case methods. At larger scale split into AuthService, CatalogService, CheckoutService and FulfillmentService; do not add ceremony solely for this exercise.

## Consistency and inventory

All writes run under a single IMutationLock. Reads return snapshots. Checkout validates all requested quantities before any stock mutation, reserves units, stores a pending order, then calls the provider with order ID as its idempotency key. Holding a single lock across checkout serializes demo writes and is deliberately low throughput.

A repeated checkout key with the same payload returns the same pending session; a different payload with that key conflicts. Server prices are authoritative. Terminal transitions are idempotent: duplicate paid/failed events do not alter stock twice. Failed, expired and simulated-canceled orders restore reservations; paid orders keep units consumed. Admin stock is *available stock*, excluding current reservations.

The in-memory lock is neither a database transaction nor a multi-instance concurrency solution. It provides no rollback on process failure. Backend restart discards everything. Payment/network failure after order creation intentionally keeps a pending reservation. No automatic reservation sweeper or payment reconciliation worker is implemented. For a persistent store, these are required before deployment.

## Replace in-memory data with a database

1. Add EF Core + one provider, Dapper, or another implementation in Infrastructure.
2. Persist products, users, orders, order lines, reservations, checkout requests and processed callbacks. Add unique normalized email and (user ID, checkout key) constraints; store minor-unit currency amounts or precise decimals.
3. Convert synchronous repository APIs to async methods with CancellationToken for real I/O; update application methods accordingly.
4. Replace IMutationLock with transactional writes and optimistic concurrency/conditional stock updates. One SQL transaction must reserve stock and persist a pending order/lines/idempotency record. Use a proper unit-of-work if needed.
5. Keep provider network I/O outside long database transactions. Use durable workflow state, retries, reservation expiration, reconciliation and an outbox for subsequent notifications/fulfillment.
6. Persist event IDs and enforce unique payment session IDs. Verify session ownership, expected amount/currency, and monotonic state transitions inside transactions.
7. For document databases, model the consistency boundary and conditional updates explicitly; changing registration alone cannot give arbitrary databases identical transaction semantics.
8. Change DI bindings and run persistence/integration tests. Business API/UI contracts can remain stable.

## Security decisions

HTTP-only auth cookies; no bearer token in localStorage. Angular obtains a readable request-token cookie from /auth/csrf and sends X-XSRF-TOKEN. ASP.NET validates against its separate HTTP-only anti-forgery cookie. Refresh CSRF after login/logout because request tokens bind to the authenticated identity. All unsafe controller methods use antiforgery except the Stripe endpoint, which uses provider signature verification instead.

API authorization restricts customer/admin routes. Every customer order operation scopes to current user. Role/account changes increment a version checked on each request, revoking older sessions. Login/register are rate limited. PBKDF2-SHA256 uses random salt and fixed-time comparison. Seed credentials are demonstration-only.

Before production: replace demo accounts, adopt ASP.NET Identity or OIDC including recovery/verification, enforce HTTPS/Secure cookies/HSTS and trusted hosts, add durable data and Data Protection key storage, secret management, CSP and audit logs with PII redaction, provider SDK or hardened adapter maintenance, tax/shipping/refund workflows, order reconciliation, monitoring and CI. Ensure proxy deployment preserves host/scheme correctly and serves Angular SPA fallbacks. Server-side account/session revocation should cover logout/security events as required.
