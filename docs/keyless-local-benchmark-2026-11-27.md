# Keyless local user-resolution benchmark, 2026-11-27

Run: `dotnet run --no-build --project benchmarks/UserManagement.Benchmarks -c Release -- --database sqlite --requests 2000 --concurrency 16 --output artifacts/benchmarks/keyless-local-2026-11-27.json`.

| Scenario | Requests/s | p50 ms | p95 ms | Source calls |
| --- | ---: | ---: | ---: | ---: |
| SQLite resolution on every request | 618 | 1.414 | 2.313 | 2,000 |
| Historical in-process five-second cache baseline | 1,108,279 | 0.001 | 0.001 | 0 |

The benchmark uses an in-process direct store and cache adapter. It does not measure the User Management HTTP endpoint, current Redis network access, SQL Server, JWT validation, or the full two-service flow. It confirms the harness runs after removing key handling. Earlier benchmark reports document older keyed builds.
