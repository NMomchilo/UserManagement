# User Management Repository Split Implementation Plan

**Goal:** Make User Management and Access Control separately buildable repositories while retaining the runtime flow and mirrored middleware tests.

**Architecture:** Move User Management service, packages, tests, benchmark, and documentation into its existing repository. Copy the two small server support projects into both repositories. Use versioned NuGet packages across the repository boundary and HTTP between services.

**Tech Stack:** .NET 10, ASP.NET Core, EF Core, xUnit, local NuGet feed.

1. Copy User Management projects and the shared server support projects into the destination, excluding generated output and `.git` files.
2. Create `UserManagement.sln`, its test project, and User Management documentation, OpenAPI, benchmark instructions, and local Docker configuration.
3. Split the current test classes by service; split mixed classes into focused tests. Retain the middleware flow test in Access Control and copy it into User Management.
4. Pack Client and Contracts projects in each repository. Replace sibling source references in service, tests, sample, and combined benchmark with matching package references.
5. Build and test both solutions, pack all four NuGet projects, and verify OpenAPI JSON and local documentation links.
6. Remove User Management files from Access Control only after the destination builds and tests. Verify both repositories again and leave all changes uncommitted.
