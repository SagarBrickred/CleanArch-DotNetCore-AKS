# Production Runbook — Failure Scenarios

Each scenario below states: what happens automatically, what an operator sees in App
Insights/Log Analytics, and what to do manually.

## 1. Azure SQL is unreachable or throttled (DTU/vCore exhaustion)

**Automatic behaviour:** `EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: 10s)` in
`Infrastructure/DependencyInjection.cs` retries any transient SQL error (timeout, deadlock,
throttling, connection reset) with exponential backoff before the exception ever reaches a
controller. If all 5 retries are exhausted, the request fails with a `500` (caught by
`ExceptionHandlingMiddleware`, logged with full detail, client sees a generic problem-details body).

**Readiness probe:** `/health/ready` includes `AddSqlServer(...)` and the custom
`DatabaseHealthCheck` (which also checks for pending migrations). If SQL is down, readiness fails,
Kubernetes stops routing traffic to that pod (but liveness stays green, so the pod isn't killed and
restarted needlessly — it comes back into rotation the moment SQL recovers).

**What to check:** Log Analytics query on the `AzureDiagnostics` table (SQL diagnostic settings are
wired in `modules/sql.bicep` — `SQLInsights`, `Errors`, `Timeouts`, `Deadlocks`,
`DatabaseWaitStatistics` categories all flow there) to see whether it's throttling (scale up the SKU)
or a genuine outage (check Azure Service Health).

**Manual escalation:** If sustained, scale the SQL SKU (`GP_Gen5_2` → higher vCores) via Bicep
redeploy, or fail over to a geo-replica if one is configured (not included by default in this
template — add `Microsoft.Sql/servers/databases/failoverGroups` if cross-region DR is required).

## 2. A pod crashes or hangs

**Automatic behaviour:** liveness probe (`/health/live`, no dependency checks) fails after 3
consecutive failures (45s), Kubernetes kills and restarts the container. Because `replicas: 3` and
`PodDisruptionBudget.minAvailable: 2`, the other pods absorb traffic during the restart. The
`RollingUpdate` strategy (`maxUnavailable: 0`) means this never drops below the desired count during
a *deployment*; the PDB protects against *node drains/upgrades* taking out too many pods at once.

**What to check:** `kubectl describe pod` for the restart reason (OOMKilled → check memory limits vs.
actual usage in App Insights `performanceCounters`; liveness timeout → check for a deadlock or an
unbounded query holding a thread).

## 3. Key Vault access fails (RBAC misconfigured, network path broken, secret rotated mid-flight)

**Automatic behaviour:** `Program.cs` calls `AddAzureKeyVault` at startup, before the host builds —
if this fails, the pod **fails to start at all** (fail-fast, not fail-open with an empty connection
string). The `startupProbe` gives it up to 5 minutes (30 × 10s) before Kubernetes gives up and
restarts it, which is enough time for `DefaultAzureCredential` to retry its token acquisition a few
times. If it's still failing after that, the Deployment will show `CrashLoopBackOff`.

**What to check:** `AddAzureKeyVault` failures log to stdout/Serilog before the Application Insights
sink is even wired (chicken-and-egg — App Insights connection string is itself in Key Vault), so
check `kubectl logs` directly, and check the AKS-managed identity's role assignment
(`modules/keyvault-access.bicep`) is still "Key Vault Secrets User" on the vault.

**Health check surfacing:** once running, `/health/ready` also includes `AddAzureKeyVault(...)` so an
*ongoing* Key Vault outage (after successful startup) degrades readiness rather than crashing the pod.

## 4. Optimistic concurrency conflict (two clients update the same product)

**Automatic behaviour:** `UpdateProductCommand` requires the caller's last-read `RowVersion`. EF Core
detects a mismatch at `SaveChangesAsync` and throws `DbUpdateConcurrencyException`, which
`UpdateProductCommandHandler` translates into `ConflictException` → HTTP `409` with a clear message.
No silent data loss (no last-write-wins).

**Client action:** re-`GET` the resource, re-apply the change, resubmit with the new `RowVersion`.

## 5. Malformed/abusive traffic (accidental retry storm, DoS attempt)

**Automatic behaviour:** the app-layer `FixedWindowLimiter` (100 req/min per user or IP,
`Program.cs`) returns `429` once exceeded — this is defense-in-depth *behind* the NGINX ingress's own
`rate-limit: "100"` annotation and whatever WAF/DDoS protection sits in front of the ingress
controller (not included in this template — add Azure Front Door + WAF policy for internet-facing
production traffic).

## 6. Bad deployment (new image is broken)

**Automatic behaviour:** `readinessProbe` on the new pods fails, so `maxUnavailable: 0` /
`maxSurge: 1` means Kubernetes never routes traffic to the broken revision — old pods keep serving
100% of traffic. The pipeline's `kubectl rollout status --timeout=300s` step fails the pipeline run
after 5 minutes, so this is caught in CI, not paged at 3am.

**Manual rollback:** `kubectl rollout undo deployment/cleanarch-api -n cleanarch-prod`.

## 7. Soft-deleted data needs recovery

**Behaviour:** `DeleteProductCommandHandler` never issues a SQL `DELETE` — it sets `IsDeleted = true`.
The global query filter in `ApplicationDbContext.OnModelCreating` hides these rows from every normal
query. To recover: query with `.IgnoreQueryFilters()` (not exposed via the API by design — do this
via a direct DB session or add an admin-only restore endpoint if the business needs it) and flip
`IsDeleted` back to `false`.
