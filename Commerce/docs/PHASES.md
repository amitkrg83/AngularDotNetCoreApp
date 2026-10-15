# Phase-by-phase walkthrough

| Phase | Files to study | Verify |
|---|---|---|
| 1. Foundation | csproj references, Domain/Models.cs, Api/Program.cs | dotnet build Commerce.sln |
| 2. Repository/DI | Application/Contracts.cs, Infrastructure/MemoryRepositories.cs | Same seeded products across requests |
| 3. Authentication | AuthController in ShopControllers.cs, Program.cs cookie events, Angular core.ts/login.ts | Login/register/logout; customer admin request returns 403 |
| 4. Catalog/cart | catalog.ts, Cart service in core.ts, checkout.ts CartPage | Search/filter/sort/details and bag quantity |
| 5. Admin | admin.ts, AdminController, CommerceService product/user methods | Add/edit/deactivate and revoke account access |
| 6. Checkout/payment | CommerceService.Checkout, Payments.cs, checkout.ts CheckoutPage, orders.ts PaymentPage | Server totals, stock reserve, simulated paid/declined outcomes |
| 7. Orders | OrdersController, orders.ts OrdersPage, admin.ts | Customer isolation, payment refresh, fulfillment progression |
| 8. Verification | tests/api_smoke.py, Commerce.Checks | Run checks; then Stripe test card with your own secrets |
| 9. Persistence (future) | docs/ARCHITECTURE.md | Swap repository implementations plus real transaction semantics |

Do not implement a separate copy for each phase. The ZIP contains the combined working code; use this map to learn it incrementally.
