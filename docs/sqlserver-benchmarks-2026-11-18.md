# User Management SQL Server benchmarks — 2026-11-18

These are **single local runs** of 1,000 requests at concurrency 16 on Windows with .NET 10 and SQL Server 2022 in Docker. The database was the dedicated `UserManagementFlow20261118` test database on port 1434, with a provisioned user and unit organization. They are synthetic measurements, not production capacity estimates.

| Scenario | Throughput | p50 | p95 | Source calls |
|---|---:|---:|---:|---:|
| Direct EF Core SQL Server resolution on every request | 2,076/s | 4.951 ms | 11.523 ms | 1,000 |
| HTTP resolution through the live service on every request | 1,631/s | 6.553 ms | 11.869 ms | 1,000 |
| Bounded five-second client cache, direct source | 891,742/s | 0.001 ms | 0.001 ms | 0 |
| Bounded five-second client cache, HTTP source | 1,182,732/s | 0.001 ms | 0.001 ms | 0 |

The cache rows measure an in-process, prewarmed memory lookup. They exclude JWT validation, gateway processing, HTTP to User Management, and SQL Server work during the measured interval. The direct and HTTP scenarios use one user and one organization repeatedly. A larger and more varied workload may behave differently.

Raw results: [direct SQL Server](../benchmarks/results/2026-11-18-live/user-management-sqlserver.json) and [live HTTP service](../benchmarks/results/2026-11-18-live/user-management-http-sqlserver.json). The fixture is in `benchmarks/fixtures/live-flow/seed-um.sql`; it now targets a fresh `UserManagementFlowIsActive20261118` database for the current schema. These recorded measurements used the earlier schema and have not been rerun. The end-to-end consuming API flow report is in the Access Control repository.
