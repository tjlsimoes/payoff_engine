# PayoffEngine — Project Subject Sheet

*A structured-product pricing API in ASP.NET Core. Build a small REST service that prices two contrasting structured products through one polymorphic engine, with dependency injection and concurrent batch pricing. C# 14 / .NET 10.*

**Estimated time:** one focused day · **Prerequisites:** Phases 1–2 complete · **Companion docs:** Phase 1 & Phase 2 reference cards (section refs below point to them)

---

## Foreword

You are not building a quant library. There is no Monte Carlo, no Black–Scholes, no stochastic volatility. The whole computational core is *evaluating a payoff at maturity given a price path* — a handful of branches and arithmetic. The point of the project is the **architecture around that core**: a clean REST API, a polymorphic pricing engine resolved through DI, and a concurrent batch endpoint. The finance is the domain that makes it a real showcase instead of artificial CRUD; it is deliberately kept shallow.

By the end you will have an API you designed yourself, that prices a Barrier Reverse Convertible and an Autocallable, exposes single and batch pricing endpoints, and demonstrates the sequential-vs-concurrent async distinction on a problem that actually motivates it.

---

## Objectives

By completing PayoffEngine you will have practised, on your own design:

- Modelling request/response DTOs as `record` types and justifying that choice.
- Implementing an interface-based strategy (`IInstrumentPricer`) with two concrete pricers.
- Registering and resolving services through DI, and *choosing a lifetime with a reason*.
- Resolving all implementations of an interface via `IEnumerable<T>` (strategy-via-DI).
- Returning correct HTTP status codes (`200`, `400`) from minimal-API endpoints.
- Writing a concurrent batch endpoint with `Task.WhenAll` and explaining why it beats a sequential loop.

---

## Mandatory part

The minimum to call the project done:

| # | Deliverable | Status code(s) |
|---|-------------|----------------|
| M1 | `POST /price` — prices a single instrument, returns a `PricingResult` | `200`, `400` |
| M2 | A working `BarrierReverseConvertiblePricer` with all three redemption scenarios | — |
| M3 | DI wiring: pricer(s) registered, resolved by `InstrumentType` | — |
| M4 | `POST /price/batch` — prices many instruments concurrently with `Task.WhenAll` | `200` |

## Bonus part

Attempt only once the mandatory part is solid:

- **B1** — The `AutocallablePricer` (the second instrument, the contrast that justifies the polymorphic engine).
- **B2** — Input validation with descriptive `400` messages (empty price path, negative notional, barrier ≥ strike, etc.).
- **B3** — A `GET /instruments` endpoint listing supported instrument types (derived from the registered pricers).

---

## Domain reference (everything you need — don't go reading quant papers)

**Common parameters.** All prices are expressed as a fraction of the initial spot (so `1.00` = at-the-money, `0.70` = 70% of initial). `Notional` is the face amount (e.g. 1000). `CouponRate` is the annual coupon (e.g. `0.08`). `PricePath` is the sequence of observed prices over the term; the **last element is the price at maturity**.

**Barrier Reverse Convertible (BRC) — three scenarios at maturity. The coupon is paid in all three.**

1. Final price **≥ strike** → redeem **100% of notional** in cash. (Whether the barrier was touched is irrelevant here.)
2. Final price **< strike**, but the barrier was **never touched** during the term → still redeem **100% of notional** in cash.
3. Final price **< strike AND** barrier **touched** at any point → redeem in **share-equivalent value** (investor takes the loss below strike).

> Barrier monitoring is *continuous* ("American"): the barrier counts as breached if **any** observation in the path touches or falls below it.

**Autocallable — adds early redemption.** On each observation (each step in the path), if the price is **at or above** the autocall level (use the strike as the autocall level for simplicity), the product **terminates early** and pays notional plus *accrued* coupon (pro-rated by how far through the term you are). If it never autocalls, it falls back to BRC-style maturity logic.

> The one structural difference to be able to state in a sentence: the **BRC is a single evaluation at maturity**; the **autocallable is a path loop with an early exit**.

---

## Step-by-step

### Step 0 — Setup
- [X] `dotnet new webapi -n PayoffEngine` (minimal API style)
- [X] Project builds and runs; the default endpoint responds
- [X] `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` confirmed in the `.csproj` (Phase 1 §1, §7)

**Checkpoint:** `dotnet run` serves the template app with no errors.

> **Note — Web API template vs Empty template:**
> `dotnet new webapi` gives you a sample endpoint, Swagger/OpenAPI wired up, and `launchSettings.json` pre-configured for browser launch. `dotnet new web` (Empty) gives you a near-blank `Program.cs` with just `WebApplication.Create` and `app.Run()` — no packages, no sample code, no Swagger. For this project the Empty template is fine; you wire everything up yourself, which is the point. The trade-off: without Swagger you test endpoints via `curl` or a REST client (Bruno, Insomnia, Postman) unless you add the OpenAPI package manually. `dotnet new webapi --no-openapi` splits the difference — more scaffolding than Empty but no Swagger.

---

> **Note — Project structure in .NET:**
>
> **Flat** — all files in the root alongside `Program.cs`. Fine when the total file count stays under ~10.
>
> **Shallow folders (layer-based, scaled down)** — group by what kind of thing each file is. This is what this project uses:
> ```
> Models/      ← DTOs
> Pricers/     ← interface + implementations
> Endpoints/   ← endpoint extension methods
> Program.cs
> ```
>
> **Full layer-based** (traditional, larger projects) — same principle, more layers: `Controllers/`, `Models/`, `Services/`, `Repositories/`, `Data/`. Adding a feature means touching several folders.
>
> **Vertical slice / feature folders** (modern preference for large projects) — group by what the code *does*, not what kind of thing it is. Adding a feature means touching one folder. Scales much better than layer-based:
> ```
> Features/
>   Pricing/
>     PricingRequest.cs
>     PricingResult.cs
>     IInstrumentPricer.cs
>     PricingEndpoints.cs
>   Instruments/
>     InstrumentsEndpoints.cs
> ```
>
> For very large solutions you'd also split into multiple **projects** within a solution — e.g. a separate class library for domain logic so it can be tested and referenced independently of the web layer.

### Step 1 — Domain model (the DTOs)
- [X] `PricingRequest` record: `InstrumentType`, `Notional`, `Strike`, `Barrier`, `CouponRate`, `decimal[] PricePath`
- [X] `PricingResult` record: `Redemption`, `CouponPaid`, `Scenario` (string, for explainability), `BarrierBreached` (bool)
- [X] Both use `decimal` (not `double`) for money — Phase 1 §5
- [X] Both are `record`, not `class`

**Checkpoint — "Can I do this?":** Explain why these are `record`s and not `class`es. *(Expected: immutable data models, value equality, free `ToString` for logging — the DTO case named in Phase 1 §4c.)*

> **Note — Why `record` and not `class` for DTOs:**
>
> - **Immutability** — positional record properties are `init`-only: set during construction, not after. For a DTO you receive it, read it, and you're done — mutation is meaningless, and a `class` would give you mutable setters by default with no benefit.
> - **Value equality** — two `PricingRequest` instances with identical field values are `==` equal. A `class` gives you reference equality by default, so two objects with the same data compare as not equal. Rarely matters in production code, but makes tests much cleaner.
> - **`ToString`** — auto-generated, prints all property values. Immediately useful for logging with no extra code.
> - **Conciseness** — the primary constructor replaces a manual constructor, `get`-only properties, `Equals`/`GetHashCode` overrides, and a `ToString` override. For a pure data-carrier with no behaviour, that's the right trade.
>
> Short version: `record` says *"this type is data, not behaviour"* — which is exactly what a DTO is.

---

### Step 2 — The pricer interface
- [X] `IInstrumentPricer` with `string InstrumentType { get; }` and `PricingResult Price(PricingRequest req)`
- [X] The interface name is `I`-prefixed (Phase 1 §10)

**Checkpoint:** The interface compiles and expresses "any instrument that can price itself and knows its own type string."

---

### Step 3 — The BRC pricer (the core — M2)
- [X] `BarrierReverseConvertiblePricer : IInstrumentPricer`
- [X] `InstrumentType => "BarrierReverseConvertible"`
- [X] Coupon computed as `Notional * CouponRate`, returned in **all** branches
- [X] Final price read with the index-from-end operator: `req.PricePath[^1]` (Phase 1 idiom)
- [X] Barrier breach detected with LINQ: `req.PricePath.Any(p => p <= req.Barrier)` (Phase 1 §15)
- [X] Scenario 1: `finalPrice >= Strike` → full notional, cash
- [X] Scenario 2: below strike, not breached → full notional, cash
- [X] Scenario 3: below strike AND breached → share-equivalent redemption (`Notional * finalPrice / Strike`)
- [X] Each branch sets a distinct `Scenario` string

**Checkpoint — "Can I do this?":** Hand-trace all three scenarios on paper with a sample path (e.g. barrier `0.70`, strike `1.00`), then confirm the code agrees. Worked traces to verify against:
- Path ending `1.05`, never below `0.70` → Scenario 1, redemption = notional.
- Path ending `0.90`, min `0.80` (never ≤ `0.70`) → Scenario 2, redemption = notional.
- Path ending `0.60`, dipped to `0.65` → Scenario 3, redemption = `Notional * 0.60`.

---

### Step 4 — DI wiring & the single endpoint (M1, M3)
- [X] Register the BRC pricer against `IInstrumentPricer`
- [X] `POST /price` resolves the pricers and selects by `InstrumentType`
- [X] Unknown instrument type → `Results.BadRequest(...)` (`400`, Phase 2 §19)
- [X] Known type → `Results.Ok(pricer.Price(req))` (`200`)
- [X] Pricers registered as **Singleton** (they're stateless — Phase 2 §23)
- [X] Endpoint injects `IEnumerable<IInstrumentPricer>` to get *all* registered pricers

**Checkpoint — "Can I do this?":** Explain why the pricers are **Singleton and not Scoped** *(stateless + thread-safe → one shared instance is correct; nothing per-request to isolate)*, and what `IEnumerable<IInstrumentPricer>` **resolves to** *(every registered implementation of the interface — the strategy-pattern-via-DI move)*.

**Manual test:** `POST /price` with a BRC body returns a correct `PricingResult`; a bogus `InstrumentType` returns `400`.

> **Note — Why extension methods live in `static` classes:**
>
> Extension methods are a C# feature that lets you add methods to an existing type without modifying it or subclassing it. The call `builder.AddInstrumentPricers()` looks like an instance method on `WebApplicationBuilder`, but it's actually a static method defined elsewhere — the compiler rewrites it to `PricerExtensions.AddInstrumentPricers(builder)`.
>
> The rules: the class must be `static`, and the first parameter must be prefixed with `this` to identify the type being extended. The class being `static` is a compiler requirement (not a design choice) — extension methods cannot live in non-static classes. In practice this means your extension method files are always static classes that act as pure groupings of related methods, with no state of their own.
>
> This is the same mechanism behind LINQ (`IEnumerable<T>.Where(...)`, `Select(...)`, etc.) — all defined as extension methods in static classes in the BCL.

> **Note — How to register pricers in DI:**
>
> Each concrete pricer must be registered against the `IInstrumentPricer` interface in `Program.cs` before `builder.Build()` is called. The key call is:
> ```
> builder.Services.AddSingleton<IInstrumentPricer, ConcreteType>();
> ```
> Registering against the *interface* (not the concrete type alone) is what allows the container to collect all implementations when resolving `IEnumerable<IInstrumentPricer>`. If you registered them under their own concrete types, the container would not include them in that collection.
>
> A clean way to keep `Program.cs` thin is to extract the registrations into a `WebApplicationBuilder` extension method (e.g. `AddInstrumentPricers`) in a separate file. This mirrors the pattern used by the framework itself (`AddAuthentication`, `AddDbContext`, etc.) and makes `Program.cs` read as a wiring manifest: one line per concern, no implementation details.
>
> When a second pricer is added (e.g. `AutocallablePricer`), a second `AddSingleton<IInstrumentPricer, AutocallablePricer>()` call in the same extension method is all that's needed — the endpoint and the `IEnumerable<IInstrumentPricer>` injection pick it up automatically.

> **Note — Why Singleton is the right lifetime for pricers:**
>
> DI lifetimes in ASP.NET Core:
> - **Transient** — a new instance every time the service is requested.
> - **Scoped** — one instance per HTTP request; shared within the request, discarded after.
> - **Singleton** — one instance for the lifetime of the application; shared across all requests and threads.
>
> The pricers are Singleton because they are **stateless**: `Price(PricingRequest)` reads only from the request it receives and computes a result — it holds no mutable fields, no per-request data, nothing that could be corrupted by concurrent access. A stateless type is inherently thread-safe, so one shared instance is both correct and efficient (no allocation per request, no per-request teardown).
>
> Scoped would also work (no correctness issue), but it allocates and discards an instance on every request for no benefit. Transient has the same wasteful allocation. Singleton is the right choice: one instance, zero overhead, thread-safe by design.

---

### Step 5 — The autocallable pricer (B1 — the contrast)
- [X] `AutocallablePricer : IInstrumentPricer`, `InstrumentType => "Autocallable"`
- [X] Loop over the path; first observation `>= autocallLevel` (use `Strike`) → early redemption
- [X] Accrued coupon pro-rated: `Notional * CouponRate * (period + 1) / PricePath.Length`
- [X] Distinct scenario string encoding which period autocalled (e.g. `$"AutocalledAtPeriod{period+1}"`)
- [X] Never autocalled → fall back to BRC maturity logic
- [X] Register it alongside the BRC pricer (second `AddSingleton<IInstrumentPricer, ...>`)

**Checkpoint — "Can I do this?":** State the **one structural difference** between the two pricers in a sentence. *(BRC = single maturity evaluation; autocallable = path loop with early exit.)*

---

### Step 6 — Batch endpoint with Task.WhenAll (M4)
- [X] `POST /price/batch` accepts `PricingRequest[]`
- [X] A local `async Task<PricingResult> PriceOneAsync(...)` wraps a single price; put an `await Task.Delay(50)` in as a stand-in for slow I/O / heavy compute
- [X] Start all tasks then await as a group: `await Task.WhenAll(requests.Select(PriceOneAsync))`
- [X] Returns `200` with the array of results

**Checkpoint — "Can I do this?":** Explain why `Task.WhenAll` over the batch is faster than awaiting each price in a loop, and tie it to *why async exists*. *(Independent operations started together overlap their wait time instead of summing it; `await` frees the thread during each delay — Phase 2 §24.)*

**Manual test:** batch of, say, 20 mixed instruments returns in roughly one `Task.Delay` worth of time, not 20×.

---

### Step 7 — Bonus polish (B2, B3)
- [X] **B2** — Validate input before pricing: non-empty `PricePath`, positive `Notional`, `Barrier` and `Strike` in sane ranges; return descriptive `400`s
- [X] **B3** — `GET /instruments` returns the supported type strings, derived from the injected `IEnumerable<IInstrumentPricer>` (not hardcoded)

**Checkpoint:** Bad input is rejected at the boundary with a `400` and a message that says what's wrong; `GET /instruments` lists both types and would automatically include a third pricer if you added one.

---

## Acceptance criteria (definition of done)

Mandatory part passes when **all** of these hold:

1. `POST /price` prices a BRC correctly across all three scenarios and returns `400` for an unknown instrument.
2. The BRC pricer's three branches match your hand-traced expectations.
3. Pricers are resolved through DI by type, registered Singleton, injected as `IEnumerable<IInstrumentPricer>`.
4. `POST /price/batch` prices concurrently (measurably faster than sequential) via `Task.WhenAll`.
5. The project builds clean with nullable reference types enabled and no warnings.

---

## Self-evaluation — defend your build

Answer all six without notes (this is the Phase 3 self-check):

1. State the three BRC redemption scenarios and when the coupon is paid.
2. Name the one structural difference between the BRC and autocallable pricers.
3. Why are the pricers registered Singleton? (Tie to statelessness, Phase 2 §23.)
4. What does `IEnumerable<IInstrumentPricer>` resolve to, and why is that a clean strategy pattern?
5. Justify `Task.WhenAll` over the batch in terms of "why async exists" (Phase 2 §24).
6. Deliver the concurrency and sockets *narratives* in 2–3 sentences each (the talking points you keep from the original Phase 3).

A clean pass means you have a finance-domain API built from your own design, the DI/async architecture exercised on your own code, and the interview narrative intact. Then into the Phase 4 drills.

---

## Constraints & notes

- **Allowed:** the full .NET 10 BCL, minimal APIs or controllers (minimal is lighter here), `decimal` arithmetic.
- **Out of scope on purpose:** any stochastic pricing model, a database (the engine is pure computation — no EF Core needed unless you *want* to persist requests as a stretch), authentication.
- **Money type:** use `decimal` throughout, never `double` — you're modelling currency (Phase 1 §5).
- **Keep endpoints thin:** validation and the pricing call belong at the boundary; the payoff logic lives in the pricers (Phase 2 conventions).
- If the day is time-pressed, **ship M1–M4 with the BRC only**; the autocallable is the clean cut.


---


# PayoffEngine — Extension Subject Sheet (Phase 6 / Day 12)

*Extends the completed PayoffEngine (Phase 3) with three tracks: **Testing**, **Persistence**, and **Production Readiness**. C# 14 / .NET 10. Companion docs: `payoffengine-subject-sheet.md` (original build), Phase 1 & 2 reference cards (section refs below point to them).*

**Prerequisites:** PayoffEngine mandatory + bonus (B2/B3) complete — confirmed done.
**Estimated time:** flexible across however many days you have; each track is independently shippable and ordered by interview payoff, not by difficulty.

---

## Objectives

By completing this extension you will have practised:

- Writing focused unit tests (`xUnit`, `[Theory]`/`[Fact]`) against pure business logic, independent of the HTTP layer.
- Designing an EF Core entity and `DbContext` from scratch (not following a course), running a migration, and wiring persistence into an existing endpoint.
- Adding a read endpoint with filtering (`GET /history?instrumentType=...`) — a second, different endpoint shape from the original pricing ones.
- Documenting an API with OpenAPI/Swagger, adding structured logging, and (optionally) a minimal auth gate — the "is this ready for someone else to hit" layer.

---

## Track A — Testing (xUnit)

*Do this first. ~60–90 minutes.*

### A1 — Test project setup
- [X] `dotnet new xunit -n PayoffEngine.Tests`, add a project reference to `PayoffEngine`
- [X] `dotnet add reference ../PayoffEngine/PayoffEngine.csproj`
- [X] Confirm `dotnet test` runs (even with zero real tests yet)

### A2 — BRC scenario tests
- [X] One `[Fact]` (or a `[Theory]` with `[InlineData]`) per scenario, using the **exact worked traces from the original subject sheet** as your expected values:
  - Path ending `1.05`, never below `0.70` → Scenario 1, redemption = notional
  - Path ending `0.90`, min `0.80` → Scenario 2, redemption = notional
  - Path ending `0.60`, dipped to `0.65` → Scenario 3, redemption = `Notional * 0.60`
- [X] Assert on `Scenario` string *and* `Redemption`/`CouponPaid`/`BarrierBreached` — not just one field
- [X] Test the coupon is paid identically in all three (the defining BRC feature)

```csharp
public class BarrierReverseConvertiblePricerTests
{
    private readonly BarrierReverseConvertiblePricer _pricer = new();

    [Fact]
    public void FinalPriceAboveStrike_RedeemsFullNotional()
    {
        var req = new PricingRequest("BarrierReverseConvertible", 1000, 1.00m, 0.70m, 0.08m,
            new decimal[] { 0.95m, 1.02m, 1.05m });

        var result = _pricer.Price(req);

        Assert.Equal("AtOrAboveStrike", result.Scenario);
        Assert.Equal(1000m, result.Redemption);
        Assert.Equal(80m, result.CouponPaid);
        Assert.False(result.BarrierBreached);
    }
    // ... Scenario 2 and 3 as [Fact]s or a [Theory] with the three paths as InlineData
}
```

### A3 — Autocallable tests
- [X] Early-exit case: path hits the autocall level partway through → check the correct period fires, accrued coupon is pro-rated correctly
- [X] Fallback case: never autocalls → confirm it matches the BRC pricer's result for the same path (this tests the delegation, not just the loop)

### A4 — Bonus: endpoint-level tests
- [X] `WebApplicationFactory<Program>` + `HttpClient` to hit `POST /price` and `POST /price/batch` for real, asserting on status codes (`200`/`400`) rather than just the pricer logic
- [X] One test for the unknown-`InstrumentType` → `400` path

**Checkpoint — "Can I do this?":** Explain, without notes, why you tested the pricers directly rather than only through HTTP *(pure logic, no I/O, fast and isolated — HTTP tests are for the wiring, not the math)*, and what makes the pricers easy to test in the first place *(no side effects, no hidden dependencies — this is the payoff of the Singleton + interface design from Phase 3)*.

**Resources:**
- xUnit getting started: https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test
- `[Theory]`/`[InlineData]`: https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-xunit#write-your-tests
- Integration testing minimal APIs: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests

---

## Track B — Persistence (EF Core)

*Second priority. ~2–3 hours depending on how far you take `GET /history`.*

### B1 — Entity design
- [X] `PricingRecord` entity: `Id` (Guid), the request fields you want to keep (`InstrumentType`, `Notional`, `Strike`, `Barrier`, `CouponRate`), the result fields (`Redemption`, `CouponPaid`, `Scenario`, `BarrierBreached`), and a `PricedAtUtc` timestamp
- [X] Decide: store `PricePath` as a serialized string/JSON column, or drop it (it's an input, not needed for history queries) — either is defensible, be ready to justify your choice
- [X] This is a `class`, not a `record` — EF Core entities are typically mutable, tracked reference types (contrast with the `record` DTOs, Phase 1 §4c)

### B2 — DbContext + provider
- [X] `PayoffEngineDbContext : DbContext` with `DbSet<PricingRecord> PricingRecords`
- [X] Pick a provider: **SQLite** is the pragmatic choice here (zero setup, file-based, real EF Core migrations) — Postgres/SQL Server are overkill for a local showcase project
- [X] Register in `Program.cs`: `builder.Services.AddDbContext<PayoffEngineDbContext>(opt => opt.UseSqlite(connectionString))`
- [X] Connection string in `appsettings.json` (Phase 2 §14 habit — never hardcode)

### B3 — Migration
- [X] `dotnet ef migrations add InitialCreate`
- [X] `dotnet ef database update`
- [X] Confirm the `.db` file / table exists

**Checkpoint:** Explain in one sentence what a migration actually does *(a versioned, generated diff between your model and the schema, applied to bring the DB in line)*.

### B4 — Wire persistence into `POST /price`
- [X] After pricing, save a `PricingRecord` via the injected `DbContext` and `SaveChangesAsync()`
- [X] Decide the `DbContext` lifetime: **Scoped** is correct here (per-request, unlike your stateless Singleton pricers) — a good contrast to justify out loud
- [X] Response still returns the `PricingResult` — persistence is a side effect, not a response-shape change

### B5 — `GET /history`
- [X] Returns past `PricingRecord`s, most recent first
- [X] Support at least one filter: `?instrumentType=Autocallable` and/or a date range
- [X] Use `IQueryable` and apply filters *before* `.ToListAsync()` — this is the Phase 1 §15 footgun in reverse: filter in the DB, don't pull everything into memory first

```csharp
app.MapGet("/history", async (PayoffEngineDbContext db, string? instrumentType) =>
{
    IQueryable<PricingRecord> query = db.PricingRecords.OrderByDescending(r => r.PricedAtUtc);
    if (!string.IsNullOrEmpty(instrumentType))
        query = query.Where(r => r.InstrumentType == instrumentType);
    return Results.Ok(await query.ToListAsync());
});
```

**Checkpoint — "Can I do this?":** Explain why the `DbContext` is registered Scoped while the pricers are Singleton *(DbContext holds per-request unit-of-work state and isn't thread-safe to share; the pricers are stateless — directly contrasts Phase 2 §23)*, and why the filter is applied on the `IQueryable` before `ToListAsync()` *(keeps filtering in the DB — Phase 1 §15)*.

**Resources:**
- EF Core getting started (SQLite): https://learn.microsoft.com/en-us/ef/core/get-started/overview/first-app
- EF Core migrations: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/
- DbContext lifetime in ASP.NET Core: https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/#using-dbcontext-with-dependency-injection

---

## Track C — Production readiness

*Third priority — do what fits, cut what doesn't. Each item below is independently ~15–30 minutes.*

### C1 — OpenAPI / Swagger
- [ ] .NET 10 minimal APIs ship built-in OpenAPI support (`AddOpenApi()` / `MapOpenApi()`) — confirm `/openapi/v1.json` resolves
- [ ] Optionally add Swagger UI (`Swashbuckle.AspNetCore` or the built-in Scalar UI) so the API is browsable, not just machine-readable
- [ ] Add a short `.WithSummary()`/`.WithDescription()` to each endpoint — cheap, and it reads as intentional API design

### C2 — Structured logging
- [ ] Inject `ILogger<T>` (or a logging delegate in minimal APIs) into the pricing endpoints
- [ ] Log one structured line per price: instrument type, scenario, redemption — **not** the full request/response as an unstructured blob
- [ ] Talking point: structured logging (named properties, not string concatenation) is what makes logs queryable in production (Seq/ELK/Application Insights) instead of just readable

### C3 — Health check
- [ ] `builder.Services.AddHealthChecks()`, `app.MapHealthChecks("/health")`
- [ ] Bonus: add a DB check (`AddDbContextCheck<PayoffEngineDbContext>()`) if Track B is done, so `/health` actually reflects DB connectivity

### C4 — Bonus: minimal auth
- [ ] Simplest defensible version: an API key checked in middleware (`X-Api-Key` header vs a configured value) — not full JWT/identity, that's disproportionate for this project's scope
- [ ] Talking point: name what you'd do for real auth (JWT bearer tokens, `[Authorize]`, ASP.NET Core Identity) without necessarily building it — knowing the next step matters as much as having built this particular gate

**Checkpoint:** Walk through `/health`, the OpenAPI doc, and one log line from a real request — three concrete artifacts that show the project "looks shipped."

**Resources:**
- Minimal API OpenAPI support: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview
- Health checks: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks
- Logging in .NET: https://learn.microsoft.com/en-us/dotnet/core/extensions/logging
- Minimal API authentication overview: https://learn.microsoft.com/en-us/aspnet/core/security/authentication/

---

## Acceptance criteria (definition of done)

| Track | Done when |
|---|---|
| A — Testing | `dotnet test` passes; all 3 BRC scenarios and both autocallable paths (early-exit + fallback) covered with assertions on more than one result field |
| B — Persistence | `POST /price` persists a record; `GET /history` returns it with at least one working filter; migration is committed |
| C — Production readiness | At minimum, `/health` responds and OpenAPI doc resolves; logging and auth are stretch goals |

## Self-evaluation — defend the extension

1. Why test the pricers directly instead of only through HTTP?
2. Why is the `DbContext` Scoped when the pricers are Singleton?
3. Walk through what a migration does and what happens if you change the entity later.
4. Why filter on `IQueryable` before `ToListAsync()`, and what goes wrong if you don't?
5. What's the one auth mechanism you'd reach for if this had to go to production, and why didn't you build the full version here?

A clean pass across all three tracks (or a deliberately scoped subset, if time is short) gives you a project with tested logic, real persistence, and visible production polish — a materially stronger story than PayoffEngine alone.