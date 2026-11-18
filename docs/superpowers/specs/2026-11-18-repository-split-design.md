# User Management repository split

User Management owns its service, Client and Contracts NuGet packages, domain, application, infrastructure, migrations, OpenAPI document, tests, benchmark, and local run documentation. Access Control owns its corresponding projects and the combined consuming API flow benchmark. Each repository keeps a local copy of the small `Common.Persistence` and `AdminManagement.Security` server support projects so builds have no sibling source-project references.

The consuming API integration remains HTTP based: User Management resolves users and returns role IDs; Access Control evaluates endpoint policies and role entitlements. Access Control uses a small local response type for its administrator scope HTTP lookup so its service builds without a sibling package. Each repository has a fast middleware flow test using the other repository's published Client and Contracts packages with fake clients. The combined benchmark uses the same package boundary and live service URLs. The source test project is split so no test project needs the other service's source.

The split copies files into `D:\git\back\UserManagement`, verifies both solutions and packages, then removes User Management source and documentation from `D:\git\back\ACNugServ`. No Git commit or database operation is part of the split.
