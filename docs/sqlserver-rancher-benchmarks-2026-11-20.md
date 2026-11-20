# Rancher Desktop SQL Server User Management benchmarks — 2026-11-20

These measurements used the SQL Server container in **Rancher Desktop's local Docker engine**, not a remote Rancher cluster. SQL Server was 2022 Developer Edition CU26 (16.0.4265.3) on Linux. The Release-built .NET services and benchmark client ran on Windows 10.0.22631 with .NET runtime 10.0.10 and 20 visible processors. The container had no explicit Docker CPU or memory limit. This is a synthetic single-workstation test, not a production capacity estimate.

User Management ran against a fresh, migrated `UMBench20261120A` database on `localhost,1434` and served HTTP at `http://localhost:5220`. A temporary copy of the [current fixture](../benchmarks/fixtures/live-flow/seed-um.sql), retargeted only to that database name, provisioned two users, one active UO, one role, memberships, and role assignments. `UserManagement.Benchmarks` resolved the fixture's first `oid` in that UO. No test request created a user or UO. The normal client cache had a five-second lifetime and a 100-entry limit in the benchmark harness.

Each value below is the **median of three independent runs**, each with 1,000 measured requests at concurrency 16. The benchmark checks that every response is active and belongs to the requested UO. Each run measured the source directly, then prewarmed a separate bounded client cache and measured its hits. The direct store adapter now returns the role IDs and both administrative duty flags present in the service response.

| Scenario | Median throughput | Median p50 | Median p95 | Source calls per run |
| --- | ---: | ---: | ---: | ---: |
| Direct EF Core SQL Server resolution on every request | 1,918/s | 5.259 ms | 11.196 ms | 1,000 |
| Live HTTP resolution on every request | 1,980/s | 5.810 ms | 10.273 ms | 1,000 |
| Warm bounded cache over direct source | 1,119,069/s | 0.001 ms | 0.001 ms | 0 |
| Warm bounded cache over HTTP source | 1,131,222/s | 0.001 ms | 0.001 ms | 0 |

Raw outputs: [direct SQL Server run 1](../benchmarks/results/2026-11-20-rancher/um-sqlserver-1.json), [2](../benchmarks/results/2026-11-20-rancher/um-sqlserver-2.json), [3](../benchmarks/results/2026-11-20-rancher/um-sqlserver-3.json); [live HTTP run 1](../benchmarks/results/2026-11-20-rancher/um-http-1.json), [2](../benchmarks/results/2026-11-20-rancher/um-http-2.json), [3](../benchmarks/results/2026-11-20-rancher/um-http-3.json).

The warm cache rows are **memory lookup throughput only**. They exclude JWT authentication, gateway processing, HTTP to User Management, and SQL Server work during their measured interval. They must not be read as service throughput. The direct and HTTP throughput ranges were 1,907–2,016/s and 1,627–2,022/s respectively, so the small difference between their medians is not a reliable HTTP-overhead estimate. The five-second cache bounds how long an already cached role or duty response can persist after a change if revision polling fails; this benchmark did not measure revocation timing.

The separate [Access Control and full middleware report](../../ACNugServ/docs/sqlserver-rancher-benchmarks-2026-11-20.md) shows the JWT → User Management → Access Control → controller path. In that path, the warm measured cases made zero User Management resolution calls and one Access Control decision call per request. The local identity provider and benchmark-only gateway identity are test stand-ins, not a production Entra/gateway integration. Results are limited to one machine, one service instance, one UO, and two fixture users; staging tests need a representative user/route mix, multiple instances, outages, and revocation checks.
