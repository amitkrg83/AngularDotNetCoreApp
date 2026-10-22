# Commerce — Angular + ASP.NET Core

A complete local learning MVP, expanding the earlier catalog-only scaffold. Angular 22.2 + .NET 10 controller-based API, with dummy data and no database. This ZIP is a **replacement solution**, not a patch to paste over an unknown local checkout. Unzip it into a new folder; keep your earlier project separately.

## Included

- Customer registration, login/logout, salted PBKDF2 password hashes, HTTP-only session cookies, CSRF protection, auth throttling.
- Customer/Admin roles enforced in the API as well as Angular guards. Changing role or disabling an account revokes its existing session.
- Product catalog, search, category filter, price/name sorting, product details, image fallback on catalog cards.
- Cart add/remove/quantity updates; cart is per browser session and cleared on login/logout, checkout, and page reload.
- US shipping form, server-authoritative USD totals, stock reservations, duplicate-checkout handling.
- A local payment simulator, plus a real Stripe Checkout **test-mode adapter** with raw-body HMAC verification, timestamp tolerance, amount/currency checks, and idempotent order transitions.
- Customer order history, order line/price snapshots, payment status refresh, resume pending checkout.
- Admin product add/edit/deactivate, available stock management, all-order list and fulfillment progression, customer enable/disable and role changes.
- Responsive layouts, form labels, keyboard focus, reduced-motion support, loading/error/empty states.
- 18 HTTP integration tests and 12 package-free C# checks.

Taxes, real shipping quotes, refunds, discounts, wishlists, reviews, password-reset/email delivery, shipment carrier integration and analytics are future phases. There is no real-money mode.

## 1. Prerequisites

Install **.NET 10 SDK**, not just the runtime; Node **24.15+ in the Node 24 line**; npm. Python 3 is optional for HTTP tests. On an Apple Silicon Mac, choose the macOS Arm64 SDK installer.

```bash
dotnet --version
node --version
npm --version
```

The repository includes `Commerce.sln`. You do not need to run `dotnet new sln --format` again. Open the extracted `Commerce` folder in VS Code.

## 2. Start the backend — terminal 1

From `Commerce/`:

```bash
dotnet restore Commerce.sln
dotnet build Commerce.sln
dotnet run --project backend/Commerce.Api
```

The launch profile sets Development and http://localhost:5000. For command-line environments that cannot run MSBuild worker nodes, use `dotnet build Commerce.sln -m:1 -p:BuildInParallel=false`.

## 3. Start Angular — terminal 2

From `Commerce/`:

```bash
cd frontend/commerce-web
npm ci
npm start
```

Open **http://localhost:4200**. The Angular proxy forwards `/api/**` to port 5000. Use `localhost` consistently. No CORS configuration is required for this setup. Angular serves HTML; API controllers return JSON.

## 4. Demo accounts

| Role | Email | Password |
|---|---|---|
| Customer | customer@commerce.test | CustomerDemo123! |
| Admin | admin@commerce.test | AdminDemo123! |

The login screen has buttons that fill these credentials. You can also register a new customer. **Accounts, products, orders and inventory reset on backend restart.** Registration never accepts a role from the client.

## 5. Walk through the application

1. Sign in as the customer. Filter/search products, open a product, add it to the bag, update quantity.
2. Continue to checkout, enter a US shipping address, continue to payment.
3. In the clearly labeled simulator choose successful payment, decline, or cancellation. No card data is requested.
4. View the order and payment state in Orders. Successful payments remain paid even if repeated callbacks arrive.
5. Sign out and sign in as admin. Add a product, edit its available stock, deactivate a product.
6. In Admin → Orders advance a paid order through Processing → Shipped → Delivered.
7. In Admin → Customers disable or promote an account. You cannot remove your own admin access.

## 6. Enable actual Stripe sandbox integration

Default `Payments:Mode` is `Simulated`. A simulator is not a payment gateway sandbox. Stripe mode makes real HTTPS requests to Stripe's test environment and needs your test credentials.

1. Create/use a Stripe account's sandbox and install the [Stripe CLI](https://docs.stripe.com/stripe-cli).
2. Authenticate and forward test callbacks:

```bash
stripe login
stripe listen --forward-to http://localhost:5000/api/payments/stripe/webhook
```

3. In another terminal, from `Commerce/`, store your own values. Replace placeholders; never commit actual secrets:

```bash
dotnet user-secrets set 'Payments:Mode' 'Stripe' --project backend/Commerce.Api
dotnet user-secrets set 'Stripe:SecretKey' 'sk_test_REPLACE_ME' --project backend/Commerce.Api
dotnet user-secrets set 'Stripe:WebhookSecret' 'whsec_REPLACE_WITH_STRIPE_LISTEN_VALUE' --project backend/Commerce.Api
```

4. Restart the API. Start a new checkout. Angular redirects to Stripe-hosted Checkout.
5. Use Stripe test card `4242 4242 4242 4242`, a future expiry date, and any valid test CVC. Never use a real card in this demo. For a decline use `4000 0000 0000 0002`.
6. Return to Orders. The signed `checkout.session.completed` event marks a **paid** session paid; a redirect alone does not. The page polls pending orders every five seconds.

Callbacks handled: paid `checkout.session.completed`, `checkout.session.async_payment_succeeded`, `checkout.session.async_payment_failed`, `checkout.session.expired`. Card-only checkout is currently configured.

Canceling the Stripe browser page does not cancel the provider session. The order stays Pending, with reserved stock, until it is paid or Stripe emits expiration. Resume with the order's checkout link. A gateway timeout also leaves the order reserved because the session may have been created: retry the same checkout in the same screen. The same provider key is reused. Keep both the API and CLI alive throughout testing; a backend restart loses pending orders, so callbacks for old sessions cannot be reconciled.

Switch back:

```bash
dotnet user-secrets set 'Payments:Mode' 'Simulated' --project backend/Commerce.Api
```

Restart the backend after changing mode. Live secret keys are rejected. The simulator endpoint is unavailable in Stripe mode.

## 7. Verify

```bash
dotnet build Commerce.sln
dotnet run --project backend/Commerce.Checks
```

With a freshly restarted API running in simulator mode:

```bash
python3 tests/api_smoke.py
```

In the frontend:

```bash
npm run build
```

HTTP tests create temporary products, users and orders in memory; restart the backend afterward. The C# checks verify callback signing/tampering/replay tolerance, live-event rejection, paid-state handling, password hashes, Stripe cents conversion and provider idempotency using a fake HTTP handler. They do not make a real Stripe transaction.

## Architecture and future database

See [Commerce/docs/ARCHITECTURE.md](Commerce/docs/ARCHITECTURE.md), [Commerce/docs/PHASES.md](Commerce/docs/PHASES.md), and [Commerce/docs/API.md](Commerce/docs/API.md).

This is a local demonstration, not a production-ready store. The application deliberately refuses non-Development hosting while it contains seeded passwords and volatile storage. No taxes are calculated and demo shipping is free. External product photos and optional fonts require internet access; system fonts and product-card image fallback work if unavailable. Do not collect real personal or card data.
