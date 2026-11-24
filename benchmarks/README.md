# User Management benchmark

The [2026-11-24 local Redis run](../docs/redis-local-benchmark-2026-11-24.md) compares direct SQLite resolution, the previous in-process cache baseline, and the new Redis cache. Start local Redis and set `UserManagementBenchmark__RedisConnectionString` to add the Redis scenario; without it, the harness runs only the historical baseline and direct lookup.

Fresh direct SQL Server, live HTTP, and warm-cache measurements are in [the 2026-11-20 Rancher Desktop report](../docs/sqlserver-rancher-benchmarks-2026-11-20.md), with links to every raw run. [The 2026-11-18 report](../docs/sqlserver-benchmarks-2026-11-18.md) is historical.

The harness in `UserManagement.Benchmarks` compares direct user resolution with the previous in-process cache as a historical baseline; set `UserManagementBenchmark__RedisConnectionString` to add the Redis scenario. Its direct store adapter returns the same active status, role IDs, and SuperAdmin/unit Admin duties as `/v1/users/resolve`. It does not run JWT validation or either consuming API middleware. Run from this repository root:

```powershell
dotnet run --project benchmarks/UserManagement.Benchmarks -c Release -- --database sqlite --requests 5000 --concurrency 16 --output artifacts/benchmarks/user-management-sqlite.json
```

For a dedicated SQL Server benchmark database, set `UserManagementBenchmark__ConnectionString` and pass `--database sqlserver`. For a running service, create the benchmark UO `33333333-3333-3333-3333-333333333333` and user `9155ff4d-2708-4770-b87e-e238645ec5df` through administrator APIs, set `UserManagementBenchmark__ClientKey`, and pass `--remote-url http://localhost:5220`. Use a separate benchmark database. Warm-cache results describe in-process cache lookup speed, not total service throughput.

The full JWT → User Management → Access Control → controller benchmark remains in the Access Control repository. This benchmark uses the `0.17.0` client NuGets from `.packages`, as does the rest of this repository. Historical results are in `docs`; they are not production capacity estimates.
