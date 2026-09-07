# Security Controls

A single reference list of every security decision baked into this template, and where to find it.

| Control | Where | Notes |
|---|---|---|
| No secrets in source control | `appsettings*.json` | Only `Development.json` has a real value, and it's `Trusted_Connection=True` LocalDB — not a secret. Prod/Staging only hold the Key Vault *URI* (public info). |
| Secrets resolved via Managed Identity, not client secret/cert | `Program.cs` (`DefaultAzureCredential`), `k8s/01-serviceaccount.yaml` (Workload Identity federation) | Nothing to leak, rotate, or expire in a config file. |
| Key Vault: RBAC (not access policies), soft-delete + purge protection, private endpoint only | `infra/bicep/modules/keyvault.bicep` | `publicNetworkAccess: 'Disabled'`; least-privilege "Key Vault Secrets User" role only, no list/manage. |
| Azure SQL: private endpoint only, TLS 1.2 minimum, AAD admin configured, Advanced Threat Protection, auditing to Log Analytics | `infra/bicep/modules/sql.bicep` | `publicNetworkAccess: 'Disabled'`; consider `azureADOnlyAuthentication: true` once every consumer is AAD-token-based. |
| AKS: private API server, AAD-only cluster access (no local admin creds), Azure RBAC, Workload Identity, Defender security monitoring | `infra/bicep/modules/aks.bicep` | `disableLocalAccounts: true`. |
| ACR: no admin user, private endpoint, image quarantine + Notary content trust + 30-day untagged-image retention | `infra/bicep/modules/acr.bicep` | Images are built *inside* ACR (`az acr build`) — source never sits on a build agent's disk as a registry credential target. |
| Container runs as non-root, read-only root filesystem, all Linux capabilities dropped | `src/CleanArch.API/Dockerfile`, `k8s/04-deployment.yaml` (`securityContext`) | UID/GID 1654, matches the Dockerfile's created user. |
| Network segmentation | `k8s/03-networkpolicy.yaml`, `infra/bicep/modules/network.bicep` | Default-deny NSGs; pod NetworkPolicy only allows ingress from the nginx-ingress namespace. |
| JWT bearer auth (Azure AD / Entra ID), role-based authorization on write endpoints | `Program.cs`, `Controllers/ProductsController.cs` | `[Authorize]` on the controller, `[Authorize(Roles = "Product.Write")]` on Create/Update/Delete — reads only need a valid token, writes need the role claim. |
| Input validation at the edge | `Commands/*/Validators`, FluentValidation pipeline behaviour | Runs *before* a handler executes — bad input never reaches the domain layer. Includes bounds on `PageSize` (max 100) to prevent unbounded-query DoS. |
| SQL injection | EF Core parameterized queries throughout; `EF.Functions.Like` for search | No raw SQL string concatenation anywhere in the codebase. |
| Optimistic concurrency | `RowVersion` (SQL `ROWVERSION`) on every entity, enforced in `UpdateProductCommandHandler` | Prevents silent lost-update races. |
| Soft delete only, no hard deletes from the API | `Product.MarkDeleted()`, global EF query filter | Matches most compliance/audit requirements; recovery path documented in RUNBOOK.md §7. |
| Security response headers | `Extensions/SecurityHeadersExtensions.cs` | CSP, X-Frame-Options, nosniff, Permissions-Policy; `Server`/`X-Powered-By` headers stripped. |
| HSTS + forced HTTPS | `Program.cs` (`UseHsts`, `UseHttpsRedirection`) | HSTS skipped in Development to avoid breaking local `http://localhost`. |
| CORS explicit allow-list, no wildcard origin in production | `Program.cs`, `appsettings.Production.json` | `Cors:AllowedOrigins` must be set; an empty list resolves to effectively closed CORS rather than open. |
| Rate limiting (app layer) | `Program.cs` (`AddRateLimiter`, fixed window, 100 req/min) | Defense-in-depth behind ingress/WAF-level limits — add Azure Front Door + WAF for internet-facing production traffic (not included in this template). |
| No stack traces / exception detail leaked to clients in production | `Middleware/ExceptionHandlingMiddleware.cs` | `detail` field is only populated when `!env.IsProduction()`. Full detail always goes to Serilog/App Insights server-side. |
| Dependency vulnerability scanning | `pipelines/azure-pipelines.yml` (`dotnet list package --vulnerable`), ACR image quarantine + Defender scan | Fails the build/gates promotion on known-vulnerable packages or images. |
| Least-privilege role assignments (AcrPull, Key Vault Secrets User) rather than Owner/Contributor | `infra/bicep/modules/aks.bicep`, `keyvault-access.bicep` | Scoped to the specific resource, specific built-in role, nothing broader. |
| Audit trail on every entity | `BaseEntity` (`CreatedBy`/`LastModifiedBy` populated from the JWT `oid`/`sub` claim via `ICurrentUserService`) | `ApplicationDbContext.SaveChangesAsync` stamps these automatically — handlers never set them manually (can't forget it). |

## Known gaps / explicitly out of scope

Being upfront about what a full production rollout would still need beyond this template:

- **WAF / DDoS protection** at the edge (Azure Front Door + WAF policy, or Application Gateway) —
  the app-layer rate limiter and NGINX ingress limits are not a substitute for this at internet scale.
- **Cross-region DR / SQL failover group** — the template configures geo-redundant *backups*
  (`requestedBackupStorageRedundancy: 'Geo'`) but not an active geo-replica with automatic failover.
- **Secrets rotation testing** — the Key Vault CSI driver is configured with `enableSecretRotation`
  and a 2-minute poll interval, but rotation should be tested end-to-end (rotate the SQL password,
  confirm pods pick it up without a restart) before relying on it in an incident.
- **SAST/DAST gates** — the pipeline has a disabled SonarCloud placeholder; wire up your org's actual
  static/dynamic analysis tooling before go-live.
- **Penetration test / third-party security review** — this template implements a strong baseline; it
  is not a substitute for an independent security assessment before handling real production traffic
  or regulated data.
