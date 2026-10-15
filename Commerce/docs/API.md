# API map

Base URL through Angular: `/api`. GET /auth/csrf first. Browser HTTP-only cookie provides identity; X-XSRF-TOKEN from the XSRF-TOKEN cookie is required for unsafe requests. Angular HttpClient sends it for relative same-origin requests.

| Method | Path | Access |
|---|---|---|
| GET | /auth/csrf | Anonymous |
| POST | /auth/register | Anonymous, CSRF, throttled |
| POST | /auth/login | Anonymous, CSRF, throttled |
| GET | /auth/me | Authenticated |
| POST | /auth/logout | Authenticated, CSRF |
| GET | /products | Authenticated |
| GET | /products/{id} | Authenticated |
| GET | /orders | Current customer's orders |
| GET | /orders/payment-mode | Authenticated |
| POST | /orders/checkout | Authenticated, CSRF, Idempotency-Key UUID header |
| POST | /orders/{id}/simulate | Order owner, CSRF, simulator mode only |
| POST | /payments/stripe/webhook | Verified Stripe signature, no cookie/CSRF |
| GET/POST | /admin/products | Admin; POST requires CSRF |
| PUT/DELETE | /admin/products/{id} | Admin, CSRF; DELETE deactivates |
| GET | /admin/orders | Admin |
| PUT | /admin/orders/{id}/fulfillment | Admin, CSRF |
| GET | /admin/users | Admin |
| PUT | /admin/users/{id} | Admin, CSRF |

## Checkout JSON

```json
{
  "items": [{"productId":"00000000-0000-0000-0000-000000000001","quantity":1}],
  "shipping":{"name":"Demo Buyer","line1":"100 Example Lane","city":"Houston","postalCode":"77001","country":"US"}
}
```

A client-supplied price/total is ignored; it is not part of the checkout contract. Response is `{orderId,url,mode}`. Money is USD with two decimal places; shipping is free and no taxes are calculated in this demo.

Simulated outcome body: `{"outcome":"Paid"}`, `Failed`, or `Canceled`. Fulfillment body: `{"status":"Processing"}`, then `Shipped`, then `Delivered`. User update: `{"role":"Customer","active":false}`.

Application errors use ProblemDetails with HTTP 400/401/403/404/409/502 as appropriate; auth throttling returns 429. Framework validation responses can contain an `errors` object. POST product success returns 201; successful deletes/updates without a body return 204.
