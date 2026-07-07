# Sensor Readings Service

[نسخه فارسی](README.fa.md)

A small backend service that ingests numeric sensor readings from `data/readings.jsonl`,
cleans duplicate and invalid records, and exposes time-based aggregation of the data
through an HTTP API with Swagger.

Built with **.NET 10** (C#), ASP.NET Core and xUnit.

## How to run

You need the .NET 10 SDK (https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/SensorReadings.Api
```

The service ingests the file once at startup (you will see the count report in the
console logs) and then serves requests. Swagger UI is available at:

```
http://localhost:5280/swagger
```

The path to the input file is configured in `src/SensorReadings.Api/appsettings.json`
(`Readings:FilePath`, relative to the API project directory).

## How to run the tests

```bash
dotnet test
```

There are 28 unit tests covering validation, deduplication and aggregation. They test
the domain project only and need no file, database or web host. Each test class starts
with a comment listing the assumptions it verifies.

## API

### `GET /api/aggregations`

Aggregates the readings of one device/metric into fixed-size time buckets.

| Parameter | Example | Notes |
|---|---|---|
| `deviceId` | `PUMP-01` | required |
| `metric` | `temperature` | required |
| `from` | `2025-06-01T08:00:00Z` | inclusive |
| `to` | `2025-06-01T08:35:00Z` | exclusive |
| `bucketSeconds` | `300` | optional, default 60 |

Example:

```
GET /api/aggregations?deviceId=PUMP-01&metric=temperature&from=2025-06-01T08:00:00Z&to=2025-06-01T08:35:00Z&bucketSeconds=300
```

Returns one entry per bucket with `bucketStart`, `count`, `average`, `min` and `max`.

Contract decisions (all covered by tests):

- The range is half-open `[from, to)`: a reading exactly at `from` is included, one
  exactly at `to` is not. This way consecutive queries never double-count a reading.
- Buckets are aligned to `from`; a reading sitting exactly on a bucket boundary belongs
  to the bucket that starts there. If the range is not divisible by the bucket size,
  the last bucket is simply cut short by `to`.
- **Empty buckets are returned with `count: 0`** and null statistics, so the caller
  always gets a contiguous series that can be charted directly without filling gaps.
- A query may produce at most 10,000 buckets; anything above that returns 400. This
  protects the service from a huge range combined with a tiny bucket size.
- Bad input (inverted range, non-positive bucket size) returns 400 with a message.

### `GET /api/ingestion/report`

The count report of the startup ingestion run. For the provided file it returns:

```json
{
  "totalLines": 2150,
  "storedReadings": 2101,
  "duplicatesRemoved": 38,
  "invalidRejected": 11,
  "rejectionsByReason": {
    "MalformedLine": 1,
    "MissingDeviceId": 2,
    "MissingMetric": 1,
    "InvalidTimestamp": 2,
    "MissingOrNonNumericValue": 2,
    "ImplausibleValue": 2,
    "MissingSeq": 1
  }
}
```

The invariant `totalLines = storedReadings + duplicatesRemoved + invalidRejected` always
holds. The same summary is logged at the end of processing, and every rejected line and
every conflicting duplicate is logged individually with its line number.

## What I found in the data

Before writing any code I went through the file to see what "messy" actually meant here.
Roughly 2,150 lines, and:

- **38 exact-key duplicates** — same `(deviceId, metric, ts, seq)`. 30 of them also have
  the same value (plain resends), but **8 carry a different value** for the same key,
  which forces an explicit conflict policy (see below).
- **1 truncated line** that is not valid JSON.
- **2 broken timestamps**: one without a timezone (`2025-06-01T08:00:05`) and one
  impossible date (`2025-06-31T08:04:10Z` — June has 30 days).
- **2 sentinel values**: a temperature of `1000000` and a vibration of `-9999`. These are
  obviously device error markers, not measurements.
- **Missing/null fields**: a null `value`, a null `metric`, a missing and an empty
  `deviceId`, a null `seq`, and a `value` of the string `"NaN"`.
- The file is **not sorted by time** (about half of the adjacent line pairs go backwards).

## Cleaning policies and why

- **Duplicates — first occurrence wins.** The task defines identity as the quadruple
  `(deviceId, metric, ts, seq)`, so a second arrival of the same key is dropped. For the
  8 conflicting duplicates there is no way to know which value is the "true" one, so I
  picked a deterministic rule (keep the first, which in a streaming scenario is the one
  that arrived first) and log a warning with the line number whenever the dropped copy
  had a different value. Ingestion is therefore idempotent: feeding the same file twice
  stores nothing new.
- **Invalid records are rejected, counted per reason and logged — never crash.** A record
  must have a non-empty `deviceId` and `metric`, a strict ISO-8601 UTC timestamp (an
  explicit `Z`; a timestamp without a timezone is ambiguous, so I reject it rather than
  guess), a finite numeric `value` and a non-negative integer `seq`.
- **Sentinel values are rejected as implausible.** All real values in this dataset live
  in small ranges (temperature ~60–80, pressure ~0–15, vibration ~0–7), so I use a coarse
  sanity bound of |value| ≤ 1000. Keeping `1000000` in the data would make every average
  useless. In a production system this bound would be configured per metric instead of
  being a single constant.
- **Out-of-order data**: nothing needs to be pre-sorted. The aggregator computes the
  bucket index from the timestamp itself, so input order is irrelevant (there is a test
  asserting shuffled and sorted input give the same result).

## Architecture

Three small projects plus tests, dependencies pointing inwards:

```
SensorReadings.Domain          <- entities, validation, dedup policy, aggregation, ports
SensorReadings.Infrastructure  <- jsonl file source, in-memory repository
SensorReadings.Api             <- controllers, DTOs, swagger, composition root
tests/SensorReadings.Domain.Tests
```

I went with a classic layered/clean style, but deliberately small: no MediatR, no generic
repositories, no CQRS. For a service of this size those would be ceremony, not
architecture. What I did keep strict:

- **All business rules live in the domain.** Validation is inside `SensorReading.TryCreate`
  (an invalid `SensorReading` cannot exist), the duplicate policy is `ReadingDeduplicator`,
  and the bucket math is `TimeBucketAggregator`. Controllers only bind/translate HTTP, and
  the repository is a dumb store — it never decides what is a duplicate.
- **The domain knows nothing about files, JSON or HTTP.** It depends on two small ports,
  `IReadingSource` and `IReadingRepository`. That is also why the tests need no
  infrastructure at all.

Extensibility, concretely:

- *New input source* (broker, HTTP feed): implement `IReadingSource`, register it in
  `Program.cs`. `IngestionService` and everything below it stay untouched.
- *New storage* (SQLite, Postgres): implement `IReadingRepository`.
- *New aggregation* (sum, median, percentile): the bucket grouping and the statistics are
  computed in exactly one place (`TimeBucketAggregator`), so a new statistic is a change
  in that one file plus the DTO. I considered a strategy interface per aggregate function
  but decided it wasn't paying for itself yet — count/avg/min/max share one pass over the
  data, and a median would need the per-bucket values anyway, which is a local change.

## Storage choice

**In-memory** (a dictionary of `(deviceId, metric)` → list of readings).

Reasons: the dataset is ~2,000 rows and read-only after startup; there is no persistence
requirement; and any database here would only add setup friction for the reviewer.
Because the repository is behind an interface, swapping in SQLite later is a one-file,
one-registration change — the ingestion and aggregation logic would not notice.

Trade-offs I'm accepting:

- Data is re-ingested on every start (cheap at this size, and it makes runs reproducible).
- Grouping by device/metric means a query only scans its own series. Within a series the
  range filter is a linear scan, O(n) per query — fine for thousands of readings. With
  millions I would keep each series sorted and binary-search the range boundaries, or move
  to a real store with an index on `(deviceId, metric, ts)`.
- Ingestion itself is streaming (`File.ReadLines`, line by line) with O(1) memory besides
  the stored result and the dedup key set.

## Assumptions

- The reading with an empty `deviceId` (`""`) is invalid rather than a legitimate device
  with an odd name.
- Timestamps must be UTC with an explicit `Z`, per the task's field definition.
- Seconds-level timestamp precision (the format in the file); fractional seconds would be
  a one-line format change.
- Duplicate detection state is per ingestion run, which is enough here because ingestion
  happens once at startup from a single source.
