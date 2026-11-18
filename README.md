# User Management

This .NET 10 repository contains the User Management service and the reusable `UserManagement.Client` and `UserManagement.Contracts` NuGet packages. It owns users, unit organizations, memberships, business roles, role assignments, and SuperAdmin and unit Admin duties. Access Control is a separate service: it receives role IDs from the consuming API and evaluates endpoint policies and entitlements.

## Request flow

The consuming API validates the Entra JWT and its trusted gateway organization assertion. `UserManagement.Client` reads the authenticated user's `oid`, application tenant, and gateway unit organization, then calls `POST /v1/users/resolve`. The service reads an **existing** user and membership; requests do not create them. Its response contains active status and role IDs. The client caches the response for at most five seconds, with revision polling, and adds the role IDs to `HttpContext.User` for the current request. The consuming API then invokes Access Control middleware and continues to its controller only on Permit. The JWT is never modified.

Anonymous requests pass through User Management middleware. An authenticated request without a valid `oid`, delegated scope, trusted gateway organization, active user, or membership is denied before Access Control middleware. New users and organizations are created by administrator operations. A user starts with no role unless an administrator assigns existing role IDs. User and role status changes use their `/active` endpoints; there are no separate user or role DELETE operations.

A SuperAdmin can create a tenant user without a unit organization using `POST /v1/users/{oid}?tenantId=default` and a body such as `{ "displayName": "Example User", "email": null }`. That user appears in the tenant user list with no memberships. To give the user protected access, an administrator must first assign an existing unit organization with `PUT /v1/users/{oid}/memberships/{unitOrganizationId}?tenantId=default`, then assign role IDs with `PUT /v1/users/{oid}/roles?tenantId=default&unitOrganizationId=<guid>`. A UO Admin can still create a user directly in a UO by supplying `unitOrganizationId` on the create call. Administrator user GET responses show the assigned tenant `isSuperAdmin` duty and each membership's `isAdmin` duty. An unscoped single-user GET requires SuperAdmin; a UO-scoped GET allows that UO's Admin and shows only that membership. The `/resolve` response used by middleware does not include these administration fields.

## Run locally

Install the .NET 10 SDK and Docker. The dedicated local SQL Server in this repository uses port **1434** to avoid a conflict with Access Control's local SQL Server on port 1433. Development startup applies the first-version EF migration to the configured fresh database. The Development connection now points to `UserManagementIsActiveLocalDev`; an earlier local database with the replaced initial migration is not upgraded automatically.

```powershell
docker compose up -d sqlserver
dotnet run --project src/UserManagement.Service --launch-profile http
```

Open `http://localhost:5220/swagger`. `GET /health` checks liveness; `GET /ready` checks database connectivity. The example connection string and credentials are in `src/UserManagement.Service/appsettings.Development.json`; replace these values outside Development. Administrator API calls require both the administrator service key and a validated Entra bearer token. Client resolution calls use a separate scoped client key. The first SuperAdmin is not provisioned automatically or by a tool in this repository.

## Use the NuGet packages

```powershell
dotnet pack src/UserManagement.Contracts -c Release -o artifacts/packages
dotnet pack src/UserManagement.Client -c Release -o artifacts/packages
```

Install matching package versions in a consuming ASP.NET Core API. Validate the Entra JWT and trusted gateway assertion before `app.UseUserManagement()`. Register with `builder.Services.AddUserManagement(builder.Configuration)` and run it after `app.UseAuthentication()`. When combined with Access Control, run User Management middleware first so Access Control receives the resolved role IDs. See [the API and client guide](docs/user-management-api.md) for configuration and examples and [OpenAPI 3](src/UserManagement.Service/wwwroot/openapi/user-management.v1.json) for the REST contract.

## Build and test

The mirrored middleware flow test uses the versioned `AccessControl.Client` and `AccessControl.Contracts` packages, rather than references to Access Control source. For local development, pack those packages into the sibling Access Control repository's `artifacts/packages` folder before restoring this solution. `NuGet.config` points to that local feed; CI can use a published package feed instead.

```powershell
dotnet test UserManagement.sln -c Release
dotnet run --project benchmarks/UserManagement.Benchmarks -c Release -- --database sqlite --requests 5000 --concurrency 16
```

The [benchmark guide](benchmarks/README.md) covers local SQLite, SQL Server, and live HTTP scenarios. Tests use SQLite and in-process HTTP. Use a separate SQL Server staging environment to validate migrations, cache revocation, and load before production use. The small `Common.Persistence` and `AdminManagement.Security` projects are maintained locally in this repository as server-only support code.
