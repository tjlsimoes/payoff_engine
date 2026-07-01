# PayoffEngine

A structured-product pricing REST API built with ASP.NET Core minimal APIs. It prices two contrasting instruments — a **Barrier Reverse Convertible** and an **Autocallable** — through a single polymorphic engine, with support for concurrent batch pricing.

**Stack:** C# 14 · .NET 10 · ASP.NET Core minimal APIs

---

## Getting started

```bash
dotnet run
```

The API listens on `http://localhost:5116` by default.

---

## Endpoints

### `GET /instruments`

Returns the list of supported instrument types.

```bash
curl http://localhost:5116/instruments
```

```json
["BarrierReverseConvertible", "Autocallable"]
```

---

### `POST /price`

Prices a single instrument. Returns `200` with a `PricingResult`, or `400` if the instrument type is unknown.

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
curl -X POST http://localhost:5000/price \
  -H "Content-Type: application/json" \
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
curl -X POST http://localhost:5000/price \
  -H "Content-Type: application/json" \
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

Prices an array of instruments concurrently. Always returns `200` with an array of `PricingResult` objects in the same order as the input.

```bash
curl -X POST http://localhost:5000/price/batch \
  -H "Content-Type: application/json" \
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
- **DI lifetimes** — pricers are registered as `Singleton`. They are stateless and thread-safe, so a single shared instance per type is correct and efficient.
- **Concurrent batch pricing** — `POST /price/batch` starts all pricing tasks simultaneously with `Task.WhenAll`, so the batch completes in the time of a single pricing operation rather than the sum of all.
