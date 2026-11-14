# An Roastery — Simulated payments

Fake checkout for the React admin. No MoMo / VNPay / Stripe keys or merchant secrets.

## Methods

| Method | Behavior |
|--------|----------|
| `Cash` | Immediate **Paid**. Closes order, frees table if idle. |
| `Card` | Same as Cash (simulated POS approve). |
| `MobileSim` | Creates **Pending** payment + `checkoutUrl` / `qrPayload`. Confirm via page, API, or callback. |
| `Mobile` | Alias for `MobileSim` (existing React PayPage). |

Canonical statuses: **Pending** → **Paid** | **Failed** (API also exposes `legacyStatus` `PendingMobile` / `Completed` for older FE).

Stock was already deducted when kitchen marked the order **Ready**; payment never deducts again.

Paid MobileSim / Cash / Card amounts roll into the open cash shift (`GET/POST /api/shift*`) when the cashier closes the shift.

## Local happy path (no ngrok)

```bash
# API (CoffeeShop/)
dotnet run --launch-profile http
# → http://localhost:5160
```

1. Login as cashier/admin, mark an order **Ready** (seed has Ready samples).
2. `POST /api/payments` with `{ "orderId": <id>, "paymentMethod": "MobileSim" }`.
3. Open `checkoutUrl` from the response, e.g. `http://localhost:5160/pay/sim/12`.
4. Click **Giả lập thành công** or **Giả lập thất bại**.
5. Poll `GET /api/payments/12` → `status: Paid` (receipt block filled) or `Failed` (order back to Ready).

Cash / Card skip the page and return `status: Paid` immediately.

## API cheat sheet

```http
POST /api/payments
{ "orderId": 1, "paymentMethod": "MobileSim", "customerPhone": "0903100001" }

POST /api/payments/{id}/confirm
{ "success": true }

POST /api/payments/{id}/simulate-callback
{ "result": "Paid" }
# or { "success": false } / { "result": "Failed" }
# empty body ⇒ Paid

GET /api/payments/{id}
```

`simulate-callback` and `GET /pay/sim/{id}` are anonymous so a phone on a tunnel can finish payment without cookie auth.

## Optional ngrok (phone demo)

Do **not** commit authtokens.

```bash
ngrok http 5160
# Open https://<subdomain>.ngrok-free.app/pay/sim/{paymentId} on the phone
```

Set `Frontend:Origins` / `Frontend:CrossOriginCookies` in config when the React app is on another HTTPS origin.

## Demo accounts

- `newadmin@coffee.com` / `NewAdmin@123`
- `cashier@coffee.com` / `Cashier@123`
