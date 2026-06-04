# Wex.Payments — Purchase Transaction & Currency Conversion API

A production-style ASP.NET Core (.NET 10) service for the WEX coding exercise. It
**stores purchase transactions in USD** and **retrieves them converted into a
target currency** using the **U.S. Treasury Reporting Rates of Exchange** API,
applying the exchange rate active for the purchase date.

## Highlights for reviewers
1. **Data Persistence** One of the main instructions was to be portable while still persisting data. This project uses an interface IPurchaseTransactionRepository 
   to abstract a persistence layer so it can be immediately run. Another data store can easily be swapped in for it.
2. **Scalability** This project is very similar to a real project I had once where we were calling EU data for VAT taxes. A significant
   issue we were concerned with was scaling.
   Scalaing is outside of this but I did decide to include an L1 cache because:
    - There is only 170 or so countries and it's likely < 10% account for all activity
    - An api like this can be used against an entire excel workbook or a quarterly update process massively.
   The cache is there but IRL would not be the end of the scaling conversation. In fact because the system has to take into consideration
   that the present quarter can be amended without warning, only previous quarters can be cached.
3. **Observability** Otel is set up here along with structured tracing. It is going to stdout but adding the correct env vars and it should 
   output to an observability framework.
4. **Rounding** For rounding I went with AwayFromZero. It has been my experience that compliance and other cross-functional teams will want input on this.
5. **Software Hardening** Items that are considered standard in produciton for this type of service are included such as resilience, logging, call level exception handling
   via middleware. Details are below.
6. **OpenAPI** Swagger is included but for security it is not published in release mode.
7. **How to run** There is an .http file included the Wex.Payments.API project root that will allow a reviewer to easily run the api.



> Treasury dataset:
> <https://fiscaldata.treasury.gov/datasets/treasury-reporting-rates-exchange/treasury-reporting-rates-of-exchange>
>
> Endpoint used:
> `GET /services/api/fiscal_service/v1/accounting/od/rates_of_exchange`

---

## How the assignment maps to the implementation

| Requirement | Where it lives | Endpoint |
|---|---|---|
| **#1 — Store a purchase** (description ≤ 50, valid date, positive USD amount to the cent, assign unique id, persist) | `PurchaseService.StoreAsync` + `InMemoryPurchaseTransactionRepository` | `POST /api/v1/purchases` |
| **#2 — Retrieve a stored purchase converted to a currency** (returns id, description, date, original USD amount, rate used, converted amount) | `PurchaseService.GetConvertedAsync` + `TreasuryExchangeRateProvider` | `GET /api/v1/purchases/{id}/converted?currency=…` |
| Rate ≤ purchase date, within last 6 months, else error | `PurchaseService` window logic | — |
| Converted amount rounded to two decimals | `PurchaseService` (documented mode) | — |
| Plug & play — no external DB/server | in-memory repository (singleton) | — |
| Production-quality automated tests | `Wex.Payments.UnitTests`, `Wex.Payments.IntegrationTests` | — |

There is **no UI**: the assignment neither requires nor forbids one, so I scoped
this as an API-only solution. Swagger UI is included purely as a zero-setup way to
exercise the API.

---

## Smart Conversion — the exchange-rate rule

"Smart Conversion" is the term the assignment email uses for the point-in-time rate-selection rule.
It is implemented (and asserted in tests) exactly as follows:

1. **Rate direction.** Treasury quotes `exchange_rate` as *units of foreign
   currency per 1 USD*. So `converted = usd_amount × exchange_rate` — no inversion.
2. **Selection window.** Of all rates for the target currency with
   `record_date ≤ purchase_date`, take the **most recent** one, **provided** its
   date is **no more than 6 months before** the purchase date
   (`purchase_date.AddMonths(-6)`). Both bounds are inclusive.
3. **No-rate failure.** If no rate falls in that window, the purchase **cannot be
   converted** and a business error is returned (HTTP 422) — we never fall back to
   a stale or newest-available rate.
4. **Amendments / multiple rows.** The dataset can return more than one row for a
   currency in a quarter (an original plus an amended rate). Because we sort
   `-record_date` and take the first row, the "pick the latest ≤ purchase date"
   step naturally selects the amended value. We anchor the rule on `record_date`.
5. **Money types & rounding.** `exchange_rate` arrives as a **string**; it is
   parsed straight to `decimal` (never `double`). The converted amount is rounded
   **once, at the end**, to two decimals.

The window is pushed into the Treasury query itself (one call, one row back):

```
/v1/accounting/od/rates_of_exchange
  ?fields=country_currency_desc,exchange_rate,record_date
  &filter=country_currency_desc:eq:Brazil-Real,record_date:lte:2024-06-30,record_date:gte:2023-12-30
  &sort=-record_date
  &page[size]=1
```

The 6-month rule is *also* asserted in our own code/tests rather than trusting the
API filter to silently enforce business logic.

---

## Financial rigor & documented decisions

These were ambiguous in the brief; each was decided deliberately and is called out
because that is exactly what the exercise is probing:

- **`decimal` for every money and rate value — never `double`/`float`.** `decimal`
  is .NET's base-10 type and the recommended choice for monetary values: it stores
  cents and the `x.xx5` rounding midpoints *exactly*, so amounts never drift the way
  binary floating-point does (`0.1 + 0.2 != 0.3` in `double`) and the rounding mode
  below becomes a deterministic decision rather than an artifact of representation
  error. The Treasury `exchange_rate` arrives as a **string** and is parsed straight
  to `decimal`, never through a `double`. This makes the email's "mind your data
  types" concrete. (`decimal` is itself floating-point, so it doesn't remove the
  need to round — it makes the rounding exact and predictable.)
- **Rounding mode: `MidpointRounding.AwayFromZero` (half-up).** The brief says
  "rounded to two decimal places" but is silent on the mode. Half-up is the most
  literal reading of "round to the nearest cent" and the least surprising when a
  reviewer spot-checks a conversion. Banker's rounding (`ToEven`) is the equally
  defensible alternative; the point is that the choice is explicit and lives in
  one constant (`PurchaseService.Rounding`).
- **Round once, at the end.** The conversion multiplies first, then rounds, to
  avoid compounding intermediate rounding error. (This is engineering judgment,
  not a brief requirement.)
- **"6 months" = `AddMonths(-6)`**, calendar months, lower bound inclusive.
- **Currency input is the Treasury `country_currency_desc` string**, e.g.
  `Brazil-Real`, `Canada-Dollar`, `Euro Zone-Euro` — **not** an ISO 4217 code.
  The Treasury feed has no ISO codes, so accepting `"EUR"` would be wrong.
- **Stored amount must already be at cent precision.** An amount with more than
  two decimals is rejected (400) rather than silently rounded, since the brief
  states the purchase amount "must be a valid positive amount rounded to the
  nearest cent." The service also rounds defensively.
- **Future dates are allowed at storage.** Requirement #1 only mandates a valid
  date format; a future-dated purchase simply fails conversion (no rate in
  window). This avoids rejecting input the brief considers valid.

---

## Architecture

Clean-architecture layering; dependencies point inward to `Wex.Payments.Core`.

```
src/
  Wex.Payments.Core            Domain. No I/O. PurchaseTransaction, PurchaseService,
                      IExchangeRateProvider + IPurchaseTransactionRepository
                      abstractions, in-memory repo, domain exceptions.
  Wex.Payments.Infrastructure  Treasury integration only. Refit ITreasuryFiscalDataApi
                      (internal), DTOs, provider, standard resilience handler (Polly v8).
                      Public surface = one DI extension method.
  Wex.Payments.Api             Minimal API. Endpoints, request/response contracts,
                      FluentValidation (endpoint filter), ProblemDetails
                      exception middleware, HTTP logging, OpenTelemetry, Swagger UI.
tests/
  Wex.Payments.UnitTests        NUnit + Moq.
  Wex.Payments.IntegrationTests NUnit + WebApplicationFactory<Program>.
```

The domain depends only on `IExchangeRateProvider`, so the Treasury specifics
(Refit, Polly, JSON shape, string parsing) are fully swappable and never leak
into the business rule.

---

## API reference

### `POST /api/v1/purchases` — store (Requirement #1)

```json
{ "description": "Laptop", "transactionDate": "2024-06-30", "amountUsd": 100.00 }
```

`201 Created` (with `Location` header):

```json
{ "id": "a406cb97-…", "description": "Laptop", "transactionDate": "2024-06-30", "amountUsd": 100.00 }
```

### `GET /api/v1/purchases/{id}` — fetch the stored purchase

`200 OK` with the stored fields, or `404` if unknown.

### `GET /api/v1/purchases/{id}/converted?currency=Brazil-Real` — convert (Requirement #2)

`200 OK`:

```json
{
  "id": "a406cb97-…",
  "description": "Laptop",
  "transactionDate": "2024-06-30",
  "originalAmountUsd": 100.00,
  "countryCurrencyDesc": "Brazil-Real",
  "exchangeRate": 5.5,
  "exchangeRateDate": "2024-06-30",
  "convertedAmount": 550.00
}
```

### Error model (all `application/problem+json`)

| Status | Meaning | Example trigger |
|--------|---------|-----------------|
| **400** | Validation error | description > 50 chars, amount ≤ 0 or > 2 dp, missing `currency` |
| **404** | Purchase id not found | unknown `{id}` |
| **422** | **Business error** — purchase cannot be converted | no Treasury rate within 6 months on/before the purchase date |
| **502** | **Technical error** — upstream Treasury failed | Treasury 5xx / timeout after Polly retries |
| **500** | Unexpected | unhandled |

The 404/422 vs 502 split is deliberate: a *business* "can't convert this" is
distinct from a *technical* "the upstream is down," and they carry different
status codes, log levels, and remediation.

---

## Running

```bash
# from the repository root
dotnet run --project src/Wex.Payments.Api
```

Then exercise the API any of three ways:

- **Swagger UI at the root** (`http://localhost:<port>/`) — the launch URL is
  printed to the console. Enabled in every environment **except Production**.
- **`src/Wex.Payments.Api/Wex.Payments.Api.http`** — open in Visual Studio / VS Code (REST Client) /
  Rider and click *Send Request*. It runs the full store→convert flow (it
  auto-chains the returned id) plus the 400 / 404 / 422 error cases.
- **curl:**

```bash
# 1) store
ID=$(curl -s -X POST http://localhost:5121/api/v1/purchases \
  -H "Content-Type: application/json" \
  -d '{"description":"Laptop","transactionDate":"2024-06-30","amountUsd":100.00}' \
  | jq -r .id)

# 2) convert (hits the live Treasury API)
curl -s "http://localhost:5121/api/v1/purchases/$ID/converted?currency=Brazil-Real"
```

`GET /health` returns `{"status":"ok"}`.

## Testing

```bash
dotnet test                          # all
dotnet test tests/Wex.Payments.UnitTests      # unit only
dotnet test tests/Wex.Payments.IntegrationTests
```

**Unit tests** (`Wex.Payments.UnitTests`):
- `PurchaseService` — store (id assignment, trim, cent rounding), get-not-found,
  convert happy path (all fields + math), convert rounding, **422** no-rate,
  **404** missing purchase, provider-exception bubbling, arg guards.
- `TreasuryExchangeRateProvider` — happy path, empty result, malformed
  rate/date, Refit `ApiException`, `HttpRequestException`, timeout (all wrapped
  as `ExchangeRateProviderException`).
- `InMemoryPurchaseTransactionRepository` — add/get round-trip, missing, dup id.
- `StorePurchaseRequestValidator` — description length (incl. boundary 50),
  empty, non-positive amount, > 2 decimals, future date allowed.

**Integration test** (`Wex.Payments.IntegrationTests`) — boots the app in-process with
`WebApplicationFactory<Program>`, swaps `IExchangeRateProvider` for a fake (so
**no live Treasury traffic**), and asserts the full **store-then-convert** flow
plus the 400 / 404 / 422 / 502 paths and `/health`.

> The Treasury integration itself was additionally smoke-tested against the live
> API during development (`Brazil-Real` → 550.00, `Euro Zone-Euro` → 93.50,
> unknown currency → 422), confirming the filter syntax and `page[size]`
> bracket encoding work in production.

---

## Caching

Treasury rates are **quarterly and immutable once published** (only recent quarters
are ever amended), so identical lookups dominate — an ideal cache target.
`CachingExchangeRateProvider` decorates `IExchangeRateProvider`, is backed by
`IMemoryCache`, and is wired in DI so every conversion goes through it.

**Key.** The provider's operation is "latest rate with `record_date ≤ purchase_date`
within the 6-month window," so the cache is keyed by the *query inputs* —
`(currency, onOrBefore, notBefore)` — not by `record_date`, which isn't known until
after the call returns. A dedicated `record struct` key gives value equality and
namespaces our entries within the shared cache.

**TTL by `record_date` age.** A lookup's answer can only change when Treasury
publishes a newer eligible quarter or amends a recent rate — both of which only touch
the recent end of the timeline:

| Result | TTL (default) | Why |
| --- | --- | --- |
| Purchase in the **current quarter** or future-dated | _not cached_ | Rate still settling (current quarter unpublished); always hits Treasury. |
| Resolved rate from a **superseded** quarter | `HistoricalTtlHours` (24h) | Frozen — a newer quarter has already published. |
| **Recent** `record_date`, or purchase date within `RecentWindowDays` (120) of today | `RecentTtlMinutes` (60m) | Amendment-prone, and a not-yet-published quarter could later become eligible. |
| **No rate** in the window (the 422 case) | `NegativeTtlMinutes` (30m) | A future publish/amendment could create one; the sentinel avoids re-hammering Treasury. |

Current-quarter (and future-dated) lookups **bypass the cache** entirely — they always
hit Treasury and are never read from or written to it, because the applicable rate is
still settling (the current quarter isn't published yet). "Current quarter" is the
calendar quarter of today (Treasury stamps rates on calendar quarter-ends). The short
TTL still covers the **just-ended** quarter through its ~1–2 week publication lag, so a
freshly published or amended rate is picked up within `RecentTtlMinutes`.

Tunable via the `ExchangeRateCache` config section (`RecentTtlMinutes`,
`HistoricalTtlHours`, `NegativeTtlMinutes`, `RecentWindowDays`); sensible defaults
apply when the section is absent. A `TimeProvider` seam keeps the TTL logic
unit-testable (`CachingExchangeRateProviderTests`).

**Limitations & scale-out.** There is no cache-stampede guard — concurrent misses for
the same key each call Treasury until the first populates (fine at this load; a
per-key lock or `HybridCache` removes it). This is an L1, per-instance cache; scaled
out, each instance keeps its own copy. The clean upgrade is .NET's `HybridCache` with
an L2 (Redis) behind the same interface — no Core changes.

For a deeper treatment of decoupling at scale (caching vs. background refresh vs.
SQS-style async vs. edge caching), see the discussion in the project notes.

---

## Logging, resilience & observability

This service is wired for **drop-in observability on any OpenTelemetry (OTLP)
backend** — the .NET Aspire dashboard, Grafana/Tempo, Jaeger, Application Insights,
etc. — with no behavioral change to the API. The whole concern lives in the
composition root (`Program.cs`); Core and Infrastructure stay vendor-neutral.

### Structured, trace-correlated logging
- **Structured console logs.** The console logger uses the **JSON** formatter by
  default and the human-readable **`simple`** formatter in Development, both with
  `IncludeScopes`. The default host enables `ActivityTrackingOptions =
  TraceId|SpanId|ParentId`, so every log line carries the active **W3C trace id** as a
  scope — logs, traces, and error payloads all line up on one id.
- **Focused access log.** `HttpLogging` emits **one combined entry per request**
  (`CombineLogs`) with method, path, query, status, and duration. A
  `HealthCheckHttpLoggingInterceptor` suppresses it for the `/health` probe and the
  Swagger UI, keeping the log on real API traffic.
- **Domain/infra logs.** `PurchaseService` logs each store and conversion (with the
  6-month window) and warns when no rate is found; `TreasuryExchangeRateProvider` logs
  the outbound filter and any upstream error; `CachingExchangeRateProvider` logs
  hit/miss/bypass at `Debug`.
- **Errors correlate.** The `problem+json` body's `traceId` is the W3C
  `Activity.Current?.TraceId` (falling back to the node-local id), so a failed response
  points straight at its trace and log lines.

### Resilience
- **`Microsoft.Extensions.Http.Resilience`** standard pipeline (Polly v8) on the
  Treasury `HttpClient`: exponential-backoff-with-jitter retry on transient errors
  (5xx/408/429), per-attempt + total timeouts, and a failure-ratio circuit breaker.
  Retry count/delay and the per-attempt timeout are tunable via the `Treasury`
  options section, validated at startup.

### OpenTelemetry — traces, metrics & logs
Auto-instrumentation is configured in `Program.cs`:
- **Traces** — ASP.NET Core (the incoming request) and `HttpClient` (the Treasury
  call) are auto-instrumented, so a request and its upstream call form **one
  distributed trace**: the server span is the parent of the Treasury client span.
- **Metrics** — HTTP server/client instruments, the **cache hit-rate** counter
  `exchangerate.cache.lookups{result=hit|miss|bypass}`, and the **Polly resilience**
  instruments (`resilience.polly.*`). The cache counter is emitted from Infrastructure
  through the **in-box `System.Diagnostics.Metrics.Meter`** — that project takes **no
  OpenTelemetry dependency**; the API subscribes by meter name
  (`AddMeter("Wex.Payments.ExchangeRateCache")`).
- **Logs** — `ILogger` output also flows through the OpenTelemetry pipeline, carrying
  scopes (including the trace id).

**Exporters are environment-driven**, so the test suite stays offline and local dev
needs zero infrastructure:

| Environment | Traces & metrics | Logs |
| --- | --- | --- |
| **Development** | Console exporter | console logger (already trace-scoped; not re-exported, to avoid duplicate lines) |
| **Testing** | none | none — the integration suite stays offline & deterministic |
| **Otherwise** (Staging / Production / …) | OTLP | OTLP |

**Out of the box the service ships no telemetry off-box.** Development prints traces and
metrics to the console; Testing exports nothing — so no external collector is contacted
by default. Exporting to a real backend is configuration-only, via the standard OTLP
environment variables: `OTEL_EXPORTER_OTLP_ENDPOINT` for the target (default
`http://localhost:4317`) and, for any hosted/authenticated collector, an API key or token
supplied through `OTEL_EXPORTER_OTLP_HEADERS` (e.g. `Authorization=Bearer <token>` or
`api-key=<key>`). No endpoint or credentials are committed to the repo. The simplest local
target is the unauthenticated standalone **.NET Aspire dashboard** (OTLP on
`localhost:4317`/`4318`), which needs no key — run the service in a
non-Development/Production/Testing environment (e.g. `Staging`) to point at it.

No domain or infrastructure changes are required; the cross-cutting concern stays at
the edge, consistent with the rest of the architecture.

---

## Configuration (`appsettings.json`)

```json
"Treasury": {
  "BaseUrl": "https://api.fiscaldata.treasury.gov/",
  "TimeoutSeconds": 30,
  "RetryCount": 3,
  "RetryBaseDelayMs": 200
},
"ExchangeRateCache": {
  "RecentTtlMinutes": 60,
  "HistoricalTtlHours": 24,
  "NegativeTtlMinutes": 30,
  "RecentWindowDays": 120
},
"Logging": {
  "Console": {
    "FormatterName": "json",
    "FormatterOptions": { "IncludeScopes": true }
  }
}
```

`appsettings.Development.json` switches the console formatter to `simple` for readable
local output (still with scopes, so the trace id shows on every line). Observability
exporters are environment-driven (see above); the OTLP target is set with the standard
`OTEL_EXPORTER_OTLP_ENDPOINT` environment variable.
