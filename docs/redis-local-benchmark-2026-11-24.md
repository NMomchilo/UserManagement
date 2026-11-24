# Local Redis cache benchmark, 2026-11-24

Command: `UserManagementBenchmark__RedisConnectionString=localhost:6379 dotnet run --project benchmarks/UserManagement.Benchmarks -c Release -- --requests 2000 --concurrency 16 --output artifacts/benchmarks/user-management-redis-local.json`. Redis was a local `redis:7-alpine` container. The source for this run was the benchmark's in-process SQLite user store.

| Scenario | Requests/s | p50 | p95 | Source calls |
| --- | ---: | ---: | ---: | ---: |
| Direct SQLite resolution | 1,171 | 0.738 ms | 1.275 ms | 2,000 |
| Previous in-process cache | 1,317,523 | 0.001 ms | 0.001 ms | 0 |
| Redis five-second cache | 26,357 | 0.532 ms | 0.705 ms | 0 |

Redis avoided User Management lookups on warm hits, but each hit made a network round trip and was slower than the old process-local cache. This is a synthetic, single-user warm-key comparison, not a production capacity estimate or a full JWT/middleware flow measurement. The old cache result is retained only as a performance baseline; production registration now uses Redis.
