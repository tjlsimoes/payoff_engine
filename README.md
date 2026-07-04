# PayoffEngine

A structured-product pricing REST API built with ASP.NET Core minimal APIs. It prices two contrasting instruments — a **Barrier Reverse Convertible** and an **Autocallable** — through a single polymorphic engine, with support for concurrent batch pricing, SQLite-backed pricing history, and API-key protected endpoints.

**Stack:** C# 14 · .NET 10 · ASP.NET Core minimal APIs · EF Core (SQLite)

---

## Getting started

```bash
dotnet run
```

The API listens on `http://localhost:5116` by default.

On startup, pending EF Core migrations are applied automatically and the SQLite database file (`PayoffEngine.db`) is created if it doesn't exist yet.

---

## Testing

The unit and integration test suite lives in a companion repository: [payoff_engine_tests](https://github.com/tjlsimoes/payoff_engine_tests).

---

## Authentication

All endpoints except `/health`, `/openapi/*`, and `/scalar/*` require an API key on every request, passed via the `X-Api-Key` header:

```bash
curl http://localhost:5116/instruments \
  -H "X-Api-Key: local-dev-key-change-me"
```

Requests without a valid key receive `401 Unauthorized`. The expected key is read from the `ApiKey` value in `appsettings.json` — the checked-in value (`local-dev-key-change-me`) is a local-dev placeholder only; a real deployment should override it via an environment variable or `dotnet user-secrets` rather than committing a real key to source control.

All `curl` examples below include the header where it's required.

---

## Endpoints

### `GET /instruments`

Returns the list of supported instrument types.

```bash
curl http://localhost:5116/instruments \
  -H "X-Api-Key: local-dev-key-change-me"
```

```json
["BarrierReverseConvertible", "Autocallable"]
```

---

### `POST /price`

Prices a single instrument. Returns `200` with a `PricingResult`, or `400` if the instrument type or input is invalid. As a side effect, the request and its result are persisted as a `PricingRecord` for later retrieval via `GET /history`.

**Request body**

| Field | Type | Description |
|---|---|---|
| `instrumentType` | string | `"BarrierReverseConvertible"` or `"Autocallable"` |
| `notional` | integer | Face amount (e.g. `1000`) |
| `strike` | decimal | Strike level as a fraction of initial spot (e.g. `1.00`) |
| `barrier` | decimal | Barrier level as a fraction of initial spot (e.g. `0.70`) |
| `couponRate` | decimal | Annual coupon rate (e.g. `0.08` = 8%) |
| `pricePath` | decimal[] | Observed prices over the term; last element is the price at maturity |

**Example — BRC, barrier breached**

```bash
curl -X POST http://localhost:5116/price \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: local-dev-key-change-me" \
  -d '{
    "instrumentType": "BarrierReverseConvertible",
    "notional": 1000,
    "strike": 1.00,
    "barrier": 0.70,
    "couponRate": 0.08,
    "pricePath": [0.95, 0.88, 0.75, 0.68, 0.65]
  }'
```

```json
{
  "redemption": 650.00,
  "couponPaid": 80.00,
  "scenario": "BelowStrikeBarrierBreached",
  "barrierBreached": true
}
```

**Example — Autocallable, early redemption at period 3**

```bash
curl -X POST http://localhost:5116/price \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: local-dev-key-change-me" \
  -d '{
    "instrumentType": "Autocallable",
    "notional": 1000,
    "strike": 1.00,
    "barrier": 0.70,
    "couponRate": 0.08,
    "pricePath": [0.92, 0.96, 1.02, 0.98]
  }'
```

```json
{
  "redemption": 1000.00,
  "couponPaid": 60.00,
  "scenario": "AutocalledAtPeriod3",
  "barrierBreached": false
}
```

---

### `POST /price/batch`

Prices an array of instruments concurrently. Always returns `200` with an array of `PricingResult` objects in the same order as the input. Every priced instrument is persisted as a `PricingRecord`, same as `POST /price`.

```bash
curl -X POST http://localhost:5116/price/batch \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: local-dev-key-change-me" \
  -d '[
    {
      "instrumentType": "BarrierReverseConvertible",
      "notional": 1000,
      "strike": 1.00,
      "barrier": 0.70,
      "couponRate": 0.08,
      "pricePath": [1.02, 1.05, 1.08]
    },
    {
      "instrumentType": "Autocallable",
      "notional": 500,
      "strike": 1.00,
      "barrier": 0.65,
      "couponRate": 0.06,
      "pricePath": [0.88, 0.91, 0.94, 0.97]
    }
  ]'
```

---

### `GET /history`

Returns past pricing records, most recent first. Optionally filter by instrument type.

```bash
curl "http://localhost:5116/history?instrumentType=Autocallable" \
  -H "X-Api-Key: local-dev-key-change-me"
```

```json
[
  {
    "id": "0199...",
    "instrumentType": "Autocallable",
    "notional": 1000,
    "strike": 1.00,
    "barrier": 0.70,
    "couponRate": 0.08,
    "pricePath": "[0.92,0.96,1.02,0.98]",
    "redemption": 1000.00,
    "couponPaid": 60.00,
    "scenario": "AutocalledAtPeriod3",
    "barrierBreached": false,
    "pricedAtUtc": "2026-07-04T09:12:00Z"
  }
]
```

---

### `GET /health`

Reports application health, including SQLite connectivity. Does **not** require an API key — intended for infrastructure/orchestrator probes.

```bash
curl http://localhost:5116/health
```

---

### API documentation

Interactive OpenAPI docs are exposed without an API key:

- `GET /openapi/v1.json` — raw OpenAPI document.
- `GET /scalar/v1` — browsable Scalar UI.

---

## Domain overview

All prices are expressed as a fraction of the initial spot (`1.00` = at-the-money, `0.70` = 70% of initial). The coupon is always paid in full regardless of scenario.

### Barrier Reverse Convertible (BRC)

Evaluated once at maturity. Three possible outcomes:

| Scenario | Condition | Redemption |
|---|---|---|
| `AtOrAboveStrike` | Final price ≥ strike | 100% of notional |
| `BelowStrikeBarrierIntact` | Final price < strike, barrier never touched | 100% of notional |
| `BelowStrikeBarrierBreached` | Final price < strike AND barrier touched at any point | `notional × (finalPrice / strike)` |

Barrier monitoring is continuous: the barrier is considered breached if any observation in the path touches or falls below it.

### Autocallable

Loops over the price path. On each observation, if the price is at or above the strike the product terminates early and pays notional plus accrued coupon (pro-rated to that period). If no early exit occurs, falls back to BRC maturity logic.

---

## Architecture

- **Polymorphic pricing engine** — pricers implement `IInstrumentPricer`. The endpoint resolves all registered pricers via `IEnumerable<IInstrumentPricer>` and selects by `InstrumentType` at runtime (strategy pattern via DI).
- **DI lifetimes** — pricers are registered as `Singleton` (stateless, thread-safe — one shared instance per type is correct and efficient). `PayoffEngineDbContext` is registered `Scoped`, since it holds per-request unit-of-work state and isn't safe to share across requests.
- **Concurrent batch pricing** — `POST /price/batch` starts all pricing tasks simultaneously with `Task.WhenAll`, so the batch completes in the time of a single pricing operation rather than the sum of all.
- **Persistence** — every priced request/result pair is saved as a `PricingRecord` via EF Core (SQLite), queryable through `GET /history`; filters are applied on `IQueryable` before materializing the results, so filtering happens in the database, not in memory.
- **API-key middleware** — a single piece of global middleware checks `X-Api-Key` on every request, with an explicit exemption list for `/health` and the docs endpoints. This is a deliberately lightweight auth gate; a production deployment would use JWT bearer tokens or ASP.NET Core Identity instead.
- **Structured logging** — each priced request logs instrument type, scenario, and redemption as a structured line via `ILogger<T>`, rather than dumping the full request/response.
- **Health check** — `/health` includes an EF Core `AddDbContextCheck<PayoffEngineDbContext>()`, so the endpoint reflects real database connectivity, not just process liveness.
