# IsActive-only lifecycle implementation plan

**Goal:** Remove `IsDeleted` across User Management and Access Control, with one active-state operation per resource.

**Architecture:** User/role active endpoints remain the sole state toggle. UO, membership, and policy DELETE operations set `IsActive=false` because they have no duplicate toggle endpoint. Inactive records remain addressable by their stable IDs where administration requires reactivation; authorization excludes them. Both first-version migrations are regenerated for fresh databases.

**Tech Stack:** .NET 10, EF Core SQL Server/SQLite, xUnit, OpenAPI 3.

---

- [x] Add regression tests for inactive lifecycle, duplicate endpoint removal, and fail-closed authorization.
- [x] Update entity models, stores, service interfaces, and controllers.
- [x] Rebuild initial migrations and model snapshots in both repositories.
- [x] Update OpenAPI, README, and benchmarks where affected.
- [x] Run both complete test suites and validate migrations and API descriptions.

Do not commit.
