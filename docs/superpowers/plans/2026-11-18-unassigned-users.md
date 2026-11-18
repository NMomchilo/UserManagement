# Unassigned User Creation Implementation Plan

**Goal:** Let a SuperAdmin create a tenant user without a unit organization or role IDs while preserving scoped creation for UO administrators.

**Architecture:** The controller selects the existing tenant-only store path when `unitOrganizationId` is omitted. It requires SuperAdmin duty and rejects any supplied role IDs. Membership and role assignment remain separate operations, and resolution continues to require membership.

**Tech Stack:** ASP.NET Core, EF Core, xUnit, OpenAPI 3.

---

### Task 1: API behavior

- [x] Add controller tests for SuperAdmin tenant-only creation, role rejection, scoped Admin rejection, and protected resolution before membership.
- [x] Run focused tests and confirm they fail for the new behavior.
- [x] Update `src/UserManagement.Service/Controllers/UsersController.cs` to make the UO query optional, enforce the right duty, validate role IDs, and return 201 without an invalid Location for tenant-only creation.
- [x] Run focused tests again.

### Task 2: Contract and explanation

- [x] Update the User Management OpenAPI, API guide, and README for both creation modes and role assignment order.
- [x] Update Access Control flow documentation to explain why unassigned users cannot authorize protected UO endpoints.
- [x] Run both test suites and validate the OpenAPI JSON.

No commits; preserve existing work in both repositories.
