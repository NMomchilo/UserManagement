# User Management

This .NET 10 repository contains the User Management API. The `UserManagement.Client` and `UserManagement.Contracts` NuGet source and package tests live in the separate Shared repository. This service owns tenant-scoped users identified by Entra `oid`, unit organizations (UOs), UO memberships, business roles with GUID IDs, role assignments, and SuperAdmin/UO Admin duties. The separate Access Control service owns policies, entitlements, and grants to those role IDs; User Management does not decide access to business endpoints.

## Request flow

1. The consuming API authenticates the Entra JWT and receives UO ID/name headers from a trusted ingress. The token supplies the user's `oid`. The application tenant defaults to `default` when its configured tenant claim is absent. Entra `tid` is a different value.
2. For an authenticated delegated user, `UserManagement.Client` checks `oid`, the required `scp` scope, and the ingress organization headers. It calls `POST /v1/users/resolve` with tenant, UO ID, and `oid`, or uses its Redis cache. Resolution reads existing records; it never creates a user, membership, UO, or role.
3. The response contains active status, UO, role IDs, `isSuperAdmin`, and `isUnitAdmin` for the selected UO. The client adds them to a dedicated identity in `HttpContext.User` for this request. It does not modify or reissue the JWT.
4. If Access Control is installed, its middleware forwards the UO, role IDs, and duties with the endpoint decision. The controller runs only after Permit. Ordinary decision calls do not make Access Control query User Management for roles.

Anonymous requests pass through this middleware and may reach a public Access Control policy. An authenticated request without a valid `oid`, delegated scope, valid ingress UO headers, provisioned user, or active status is stopped before Access Control. An ordinary user or UO Admin needs an active membership in that UO. An active SuperAdmin can resolve any active UO in the same application tenant without membership or a business role. A request to an unknown UO does not create it. The client maps a missing user or membership to `403`, and a service outage to `503`.

The cache is keyed by application tenant, UO ID, and `oid`, stored in Redis with a five-second absolute expiration. Each consuming API instance polls the User Management revision to clear cached responses after administrator changes; expiry is the fallback if polling fails. Already cached roles or duties can therefore remain effective for up to five seconds, apart from requests already in flight.

## Users, roles, and administrator duties

An administrator explicitly creates a user with `POST /v1/users/{oid}`. Supplying an existing UO ID creates the first membership; optional `roleIds` must refer to active roles in that UO. No Reviewer or other role is assigned automatically. A SuperAdmin alone can omit `unitOrganizationId` to create a tenant user without memberships or roles. Assign a membership with `PUT /v1/users/{oid}/memberships/{unitOrganizationId}`, then assign roles with `PUT /v1/users/{oid}/roles`. Revoking a membership uses `DELETE` on that membership route. User and role deactivation use their `/active` endpoints; there are no separate user or role DELETE operations. An inactive user cannot resolve.

An active SuperAdmin can manage all UOs in their application tenant. A UO Admin can manage only their assigned, active UOs and may be an ordinary user in other UOs. Only a SuperAdmin can grant or revoke SuperAdmin and UO Admin duties, deactivate/reactivate users, and manage UOs globally. The service does **not** seed the first SuperAdmin at startup or provide a bootstrap tool; a controlled deployment seed is required before administrator REST operations can provision further administrators.

Administrator reads include `isSuperAdmin` on the user and `isAdmin` on each membership. `GET /v1/users?tenantId=default` lists users for a SuperAdmin; `GET /v1/users/by-email` searches by email; `GET /v1/users/{oid}` reads one user, optionally scoped to a UO. `GET /v1/unit-organizations/{id}/users` and `GET /v1/roles` are paginated. `POST /v1/roles` creates a business role in an existing UO with a required, immutable `code` and display name. The code is unique within tenant and UO, including disabled roles; assignments and decisions still use the role GUID. Access Control separately creates entitlements with codes and grants entitlement GUIDs to that role ID. See the [API guide](docs/user-management-api.md) and [OpenAPI 3 specification](src/UserManagement.Service/wwwroot/openapi/user-management.v1.json) for all routes, request bodies, and scope rules.

## Run locally

Install the .NET 10 SDK and Docker. From this repository root, start the SQL Server defined in [docker-compose.yml](docker-compose.yml) and then the service:

```powershell
# The six Shared 0.17.0 NuGet files and UserManagement.Client 0.18.0 are included in .packages.
docker compose up -d sqlserver
dotnet run --project src/UserManagement.Service --launch-profile http
```

To exercise `UserManagement.Client` from a consuming API, also start `docker compose up -d redis`. This repository maps its local Redis to `localhost:6380`; set the consuming API's `ConnectionStrings:LocalRedisServiceConnectionString` to `localhost:6380` in Development. The User Management service itself reads SQL Server and does not need Redis for `/v1/users/resolve`; the Redis cache lives in each consuming API through the Shared NuGet. In a production consuming API, set `ConnectionStrings:RedisServiceConnectionString` to the Redis endpoint, `Redis:KeyPrefix` to a deployment-specific prefix, and `Redis:ApplicationDatabaseIndex` to a nonnegative index. Redis keys have a five-second TTL, and Redis failures call User Management directly. Keep Redis access on trusted networks and use authentication/TLS for deployed instances.

Both service hosts call `AddSharedKeyVault` before binding service settings. Set `KeyVault:Url` to an HTTPS vault URL to enable it; leave it empty for local Development. The provider uses `DefaultAzureCredential` unless `KeyVault:TenantId`, `ClientId`, and `ClientSecret` are all supplied through a secure source. Use managed identity in deployment. A vault secret named `ConnectionStrings--UserManagement` supplies the SQL connection string; `UserManagementKeys--Clients--0--Key` supplies a client key. Configuration is loaded at startup, so restart the service after rotating a secret.

Open `http://localhost:5220/swagger`. The local SQL Server is exposed on **1434**, separate from Access Control's port 1433. The Development connection uses database `UserManagementIsActiveLocalDev` and the Docker compose `sa` password. Automatic migration runs **only in Development** when `UserManagementDatabase:ApplyMigrationsOnStartup` is true. This is a fresh first-version schema; an older database made from a replaced migration is not upgraded in place. The example settings are in [appsettings.Development.json](src/UserManagement.Service/appsettings.Development.json). Replace example keys and connection strings outside Development.

`GET /health` checks liveness; `GET /ready` checks database connectivity and returns `503` when unavailable. Neither GET probe needs an API key. Swagger UI and `/openapi/user-management.v1.json` are served only in Development. The local HTTP launch profile is for development; configure HTTPS outside Development.

`UserManagementKeys:Clients` keys authorize user resolution and revision polling; a client with `CanRegisterUnitOrganizations` can also register a UO. `UserManagementKeys:Administrators` keys are separate and accompany a validated Entra administrator token on management calls. `UserManagementKeys:AdminLookupKey` is reserved for Access Control's `GET /v1/admin/access` lookup, which also forwards that administrator token. These credentials must be distinct and at least 32 characters long.

`POST /v1/registrations/unit-organizations` uses an existing `UserManagementKeys:Clients` key when `CanRegisterUnitOrganizations` is enabled. A registration-enabled client may supply any tenant and UO; its configured `ClientId` identifies the write actor. It creates a missing UO or confirms an identical active one. A different name or inactive UO returns `409`. It does not create users, memberships, roles, or duties. A consuming API can then register its endpoint catalog with Access Control. The local Development client key is configured to allow this startup operation.

## Use the NuGet packages

The service, tests, and benchmarks use versioned Shared NuGet packages (UserManagement.Client 0.18.0; others 0.17.0). This repository includes the seven package files in `.packages`: `AccessControl.Client`, `AccessControl.Contracts`, `UserManagement.Client`, `UserManagement.Contracts`, `Common.Persistence`, and `AdminManagement.Security`, and `Shared.Infrastructure`. Their versions must match the project references. When Shared releases a new version, pack it, update the project references, and replace these package files. For example, after packing Shared in any location:

```powershell
Copy-Item 'C:\path\to\Shared\packages\*.nupkg' .packages\
dotnet restore UserManagement.sln
```

`NuGet.config` maps these package IDs to `.packages`; no sibling checkout or package download is needed after cloning this repository with the package files committed. Once a private feed is chosen, replace that local source with the feed URL and supply its credentials through CI secrets; do not commit credentials.

Install `UserManagement.Client` 0.18.0 and `UserManagement.Contracts` 0.17.0. Configure `UserManagement:ServiceUrl`, `ApiKey`, tenant settings, delegated scope, `UnitOrganizationIdHeaderName`, `UnitOrganizationNameHeaderName`, Redis connection/lifetime, and timeout. The consuming host validates the Entra JWT. The NuGet reads organization headers directly; ingress must strip client-supplied copies, insert exactly one ID and name, and be the only allowed path to the API. Header validation does not establish sender trust.

```csharp
builder.Services.AddUserManagement(builder.Configuration);
// Add the host's JWT authentication services as well.

app.UseRouting();
app.UseAuthentication();
// The trusted ingress supplies organization headers before this point.
app.UseUserManagement();
app.UseAccessControl(); // If AccessControl.Client is installed.
app.UseAuthorization();
app.MapControllers();
```

The Access Control middleware must run after User Management to receive the resolved role IDs and duties. The separate Access Control repository's CustomerApi sample shows client registration and ordering. Its authenticated requests receive `403` when the ingress organization headers are missing. See the [API and client guide](docs/user-management-api.md) for configuration details.

## Build and test

Clone this repository anywhere; the committed `.packages` files are used during restore. Every project in this solution uses versioned package references for Shared code, including the middleware flow test and benchmarks. Package-only tests and coverage live in Shared; this repository retains API, persistence, and flow tests. Build the Docker image from this repository root with `docker build -f src/UserManagement.Service/Dockerfile -t user-management:local .`.

```powershell
dotnet test UserManagement.sln -c Release
dotnet test tests/UserManagement.Tests -c Release --collect:"XPlat Code Coverage" --settings coverage.runsettings
dotnet run --project benchmarks/UserManagement.Benchmarks -c Release -- --database sqlite --requests 5000 --concurrency 16
```

The [benchmark guide](benchmarks/README.md) covers local SQLite, SQL Server, and live HTTP resolution; the [2026-11-20 Rancher Desktop results](docs/sqlserver-rancher-benchmarks-2026-11-20.md) record fresh live runs. The end-to-end JWT → User Management → Access Control → controller benchmark lives in the Access Control repository. Unit/in-process tests use SQLite; they do not replace SQL Server migration, revocation, and load checks in a dedicated staging environment.
