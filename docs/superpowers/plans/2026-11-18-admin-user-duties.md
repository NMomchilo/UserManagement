# Administrator User Duty Reads Implementation Plan

**Goal:** Show tenant SuperAdmin and per-UO Admin duties in user administration GET responses.

**Architecture:** Add `IsSuperAdmin` to user summaries and `IsAdmin` to membership summaries. Reuse the batched store projection for exact oid reads. A UO-scoped exact read remains accessible to its Admin; an unscoped exact read requires SuperAdmin and includes all active memberships. `/v1/users/resolve` remains unchanged.

**Tech Stack:** ASP.NET Core, EF Core, xUnit, OpenAPI 3.

---

- [x] Write failing store, controller, and HTTP tests for flags, scope, inactive records, and tenant-only users.
- [x] Add summary fields through database projection, domain, application, and contract.
- [x] Change only administrator exact GET to return `UserSummary`, with optional UO and scope authorization.
- [x] Update OpenAPI and User Management documentation.
- [x] Run all User Management tests, OpenAPI validation, and check for pending EF model changes.

Do not commit.
