# User Management benchmark

New direct SQL Server and live HTTP measurements are in [the 2026-11-18 report](../docs/sqlserver-benchmarks-2026-11-18.md).

The harness in `UserManagement.Benchmarks` compares direct user resolution with bounded client-cache hits. Run from this repository root:

```powershell
dotnet run --project benchmarks/UserManagement.Benchmarks -c Release -- --database sqlite --requests 5000 --concurrency 16 --output artifacts/benchmarks/user-management-sqlite.json
```

For a dedicated SQL Server benchmark database, set `UserManagementBenchmark__ConnectionString` and pass `--database sqlserver`. For a running service, create the benchmark UO `33333333-3333-3333-3333-333333333333` and user `9155ff4d-2708-4770-b87e-e238645ec5df` through administrator APIs, set `UserManagementBenchmark__ClientKey`, and pass `--remote-url http://localhost:5220`. Use a separate benchmark database. Warm-cache results describe in-process cache lookup speed, not total service throughput.

The full JWT → User Management → Access Control → controller benchmark remains in the Access Control repository and uses the published User Management client packages. Historical results are in `docs`; they are not production capacity estimates.
