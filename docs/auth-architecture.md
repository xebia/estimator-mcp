# Estimator MCP — Authentication Architecture

This document is for the next developer or DevOps engineer who has to **understand, maintain, deploy, or extend** the auth on this server. It explains the two side-by-side OAuth flows, the Entra app registration that backs them, the managed-identity arrangement with its current cross-tenant blocker, and the tenant-side configuration that has to be in place before a Copilot Studio agent can call the MCP endpoint.

If you just want to wire up your own Copilot Studio agent against the deployed server, skip this and read [`copilot-studio-setup.md`](copilot-studio-setup.md) instead. If you want the historical phase-by-phase plan with task checkboxes, read [`plans/msal-auth.md`](plans/msal-auth.md).

---

## 1. Goals and constraints

### What we needed
1. **SSO for the Blazor catalog editor** — Xebia developers sign in with their Xebia identity rather than the previous email-magic-link + opaque-Bearer-token flow.
2. **OAuth-protected MCP endpoint** that an M365 / Copilot Studio agent can authenticate to per the Model Context Protocol authorization spec. The MCP client must be able to **discover** the authorization server from the protected resource, not be told out of band.
3. **One identity surface** — both flows resolve against the same Xebia Entra ID app registration so users, scopes, and admin policy live in one place.
4. **No long-lived secrets in source or in a developer's keyring** — the deployed app should authenticate itself to Xebia Entra using a workload identity, not a client secret. (We had to compromise on this; see §5.)

### Why these constraints
- The MCP server is hosted by Marimer (Rocky's tenant) but consumed by **Xebia M365 users via Copilot Studio**, so the identity provider must be the Xebia tenant — Copilot Studio acquires tokens for its agent users from Xebia Entra.
- Copilot Studio MCP connectors require a discoverable OAuth flow that follows the MCP authorization spec (RFC 9728 — OAuth Protected Resource Metadata) so the connector knows where to send users for sign-in. We can't just hand-configure a token endpoint and be done.
- Xebia Entra is a separate tenant from Marimer, and the Container App lives in Marimer's subscription. That cross-tenant boundary is the source of most of the operational complexity.

---

## 2. Architecture at a glance

Two ASP.NET Core authentication schemes run side by side in the same web app:

```
            Browser users                        Copilot Studio agent
                  │                                         │
                  │ OIDC code flow (cookies)                │ OAuth 2.1 + MCP authz spec (JWT)
                  ▼                                         ▼
              ┌───────────────────────────────────────────────────┐
              │           Xebia Entra ID (single tenant)          │
              │     App registration: Estimator MCP               │
              │     Client ID: 32c976ae-e874-4126-b2d8-10bc99ae9330│
              └───────────────────────────────────────────────────┘
                  │                                         │
                  │ id_token + auth code                    │ access_token (JWT, aud=api://{client-id})
                  ▼                                         ▼
        Cookies + OpenIdConnect handler            JwtBearer handler
                  │                                         │
                  ▼                                         ▼
              Razor pages                       /mcp  +  /api/catalog/*
              (Blazor Server UI)             (BearerOnly authorization policy)
```

Wired in `src/EstimatorMcp.Web/Program.cs`:
- `AddMicrosoftIdentityWebApp(...)` — the OIDC scheme for the Blazor UI; default scheme.
- `AddMicrosoftIdentityWebApi(...)` — the JwtBearer scheme for `/mcp` and the REST API.
- `BearerOnly` authorization policy on `/mcp` and `/api/catalog/*` — pins those endpoints to the JWT scheme so unauthenticated callers receive a clean **401**, not a 302 redirect to the Xebia login page (which would break any non-browser client).

Both schemes read the same `AzureAd` config section in `appsettings.json`. The OIDC flow picks up `Instance/TenantId/ClientId/CallbackPath/Scopes`; the JWT flow picks up `TenantId/ClientId/Audience`.

---

## 3. The Xebia app registration

There is **one** app registration in the Xebia tenant. Its full identity and what each property does:

| Property | Value | Why it's set this way |
|---|---|---|
| Tenant | `3d4d17ea-1ae4-4705-947e-51369c5a5f79` (Xebia) | Source of identity for both flows. |
| Client ID | `32c976ae-e874-4126-b2d8-10bc99ae9330` | Same app registration is used as the OIDC client (Blazor UI) **and** as the protected API resource (`/mcp`). Single app reg keeps consent, secrets, and policy in one place. |
| Application ID URI | `api://32c976ae-e874-4126-b2d8-10bc99ae9330` | Becomes the `aud` claim on JWTs issued for `/mcp`. |
| Exposed scope | `access_as_user` | Single delegated scope; admin or user consent both acceptable. The MCP server only checks that this scope is present. |
| Redirect URIs (Web) | `https://localhost:5001/signin-oidc` (dev), `https://{prod-fqdn}/signin-oidc` (prod), and **one per Copilot Studio connector** that gets added by the developer who creates the connector | Entra is byte-for-byte strict — every redirect URI must be pre-registered. Copilot Studio's redirect URIs are unique per connector instance, so each developer who sets up an agent adds their own. |
| Front-channel logout URL | `https://{host}/signout-oidc` | Standard OIDC sign-out plumbing. |
| Federated credential | Trusts the **Marimer user-assigned managed identity** `2b9666a5-9d42-4d1d-a735-138d2ecc299c` as a workload-identity issuer | This *should* let the deployed app authenticate itself to Xebia Entra without any secret. Currently blocked at the Xebia tenant level — see §5. |
| Client secret(s) | One per Copilot Studio connector, plus one used by the deployed Container App as a fallback while FIC is blocked | Each connector secret is scoped to a single developer so they can be rotated independently. |
| Pre-authorized client | Azure CLI (`04b07795-8ddb-461a-bbee-02f9e1bf7b46`) for `access_as_user` | Lets developers acquire tokens via `az account get-access-token --resource api://{client-id}` for local testing without an interactive consent prompt. |

The MCP server only validates four things on an incoming token:
1. Issuer = Xebia tenant (`https://login.microsoftonline.com/{xebia-tenant-id}/v2.0`, or the v1 `sts.windows.net/{tenant-id}/` form).
2. Audience = `api://{client-id}` or the bare `{client-id}`.
3. Signature, lifetime — handled by `AddMicrosoftIdentityWebApi` against the standard Microsoft Identity metadata endpoint.
4. `scp` claim contains `access_as_user` — implicit through the framework's default scope check.

There is **no per-user or per-role authorization yet**. Any signed-in Xebia user can call any tool. This is documented as a known gap in `copilot-studio-setup.md` and `plans/msal-auth.md`.

---

## 4. The MCP authorization handshake

The MCP authorization spec layers on top of OAuth 2.1 by requiring that the protected resource publish a **Protected Resource Metadata (PRM)** document per RFC 9728, and that 401 responses point at it via `WWW-Authenticate`. This is how Copilot Studio's connector finds the authorization server without being told.

The full discovery handshake against the deployed server:

1. Connector calls `GET https://{host}/mcp` with no token.
2. Server responds **401** with `WWW-Authenticate: Bearer resource_metadata="https://{host}/.well-known/oauth-protected-resource/mcp"`.
3. Connector follows the URL and reads the PRM JSON document:
   ```json
   {
     "resource": "https://{host}/mcp",
     "authorization_servers": ["https://login.microsoftonline.com/3d4d17ea-.../v2.0"],
     "scopes_supported": ["api://32c976ae-.../access_as_user"],
     "bearer_methods_supported": ["header"]
   }
   ```
4. Connector takes the user (or its maker identity, depending on configuration) through the OAuth code flow against the Xebia authorization server, requesting the listed scope.
5. Connector retries `GET /mcp` with the resulting JWT in `Authorization: Bearer ...`. The JwtBearer handler validates and admits the request.

Three pieces of code in `Program.cs` make this work, and they are subtle enough that any future change should preserve the order:

- **`UseForwardedHeaders` is registered first.** Container Apps' ingress terminates TLS and forwards as plain HTTP, so without this, `Request.Scheme` is `http` and the PRM `resource` URL plus the `WWW-Authenticate` `resource_metadata` URL come out as `http://`, which RFC 9728 rejects and Copilot Studio refuses.
- **The PRM-rewriting middleware is registered before `UseAuthentication`.** The framework's challenge handlers set `WWW-Authenticate` themselves; if our middleware sits after `UseAuthorization` it never runs on the unwind path because the 401 short-circuits. Wrapping the auth pipeline lets us overwrite the header with the spec-required `resource_metadata="..."` form.
- **`UseStatusCodePagesWithReExecute` is gated to non-API paths.** Otherwise, a 401 from `/mcp` gets re-executed as `/not-found` and the 401 body becomes the Blazor index HTML, which is not a valid response for an MCP client — the client expects an empty body and a clean `WWW-Authenticate` header.

If any of these three are removed or reordered, the discovery handshake breaks in subtle ways that will not show up in `dotnet test` and will only surface when a real Copilot Studio connector tries to attach.

---

## 5. The cross-tenant federated-identity blocker (read this carefully)

The intent of the architecture was to use **federated workload identity** so the deployed Container App authenticates itself to the Xebia app registration without any client secret:

- A user-assigned managed identity (`2b9666a5-...`) lives in Marimer, attached to the Container App.
- A federated credential on the Xebia app registration trusts that MI's issuer.
- At runtime, `Microsoft.Identity.Web` reads `AzureAd:ClientCredentials` from `appsettings.json` (which contains a `SignedAssertionFromManagedIdentity` entry pointing at the MI) and exchanges an MI-issued token for an assertion that authenticates the app to Xebia Entra.

**This is currently blocked.** The Xebia tenant's cross-tenant access policy (Entra → External Identities → Cross-tenant access settings) does not allow inbound workload identity federation from external tenants. Token requests fail with:

> **AADSTS700236**: Entra ID tokens issued by issuer `https://sts.windows.net/da478866-394f-4ef4-8257-b720d51eaade/` may not be used for federated identity credential flows for applications or managed identities registered in this tenant.

(The issuer in the error is the Marimer tenant.) Until a Xebia tenant admin allows inbound workload identity federation from Marimer (`da478866-394f-4ef4-8257-b720d51eaade`), the deployed app falls back to a **client secret** stored as the `azure-ad-client-secret` Container App secret. Two environment variables override the appsettings entry at runtime:

- `AzureAd__ClientCredentials__0__SourceType=ClientSecret`
- `AzureAd__ClientCredentials__0__ClientSecret={secretRef}`

The MI is still attached to the Container App, and the federated credential is still configured on the app registration. The fallback is intentionally a runtime override, not a code change, so flipping back to FIC is a config-only operation.

### To re-enable FIC once the tenant policy is fixed

1. Xebia tenant admin allows inbound workload identity federation from Marimer (`da478866-394f-4ef4-8257-b720d51eaade`).
2. From the repo root:
   ```powershell
   azd env set XEBIA_APP_CLIENT_SECRET ""
   azd up
   ```
   Empty value leaves the override env vars off the Container App, so the app falls back to `appsettings.json`'s `SignedAssertionFromManagedIdentity` config.
3. Delete the now-unused client secret on the Xebia app registration (Certificates & secrets → the `azure-ad-client-secret` value).
4. Verify by signing into the Blazor UI and checking that the request succeeds with no client secret present in either appsettings or env vars.

### Until then, what to know about the secret

- Stored only as a Container App secret (`azure-ad-client-secret`), wired via the `xebiaAppClientSecret` Bicep parameter.
- **Never committed.** It's a `@secure()` Bicep parameter and is supplied via `azd env set XEBIA_APP_CLIENT_SECRET <value>` — `azd` stores environment variables encrypted in `.azure/{env}/.env` which is gitignored.
- Has whatever expiry the operator picked when generating it on the Xebia app registration. **Rotation is manual** — no monitor will tell you it's about to expire.

---

## 6. Deployment topology

Authoritative source: `infra/main.bicep` and `infra/resources.bicep`. Driven by `azd up`.

| Resource | Role | Notes |
|---|---|---|
| Container App `ca-{token}` | Hosts the web app + MCP endpoint | `maxReplicas=1` (single writer for SQLite). Ingress on `targetPort: 8080`, `transport: 'http'`, TLS terminated at ingress. |
| Container Apps Environment `cae-{token}` | Runtime + log routing | Logs to Log Analytics. |
| User-assigned MI (in Marimer, **passed in by ARM resource ID**) | Identity surface for the Container App | Attached via `userAssignedIdentities` in `containerApp.identity`. The MI itself is **not** created by the Bicep — it's passed as the `userAssignedIdentityResourceId` parameter so the deployment doesn't have to assume the MI lives in the same RG or subscription. |
| Storage account + Azure Files share | SQLite + log persistence | Mounted at `/data`. Azure Files (SMB) doesn't support `flock()`, so the app uses SQLite's `vfs=unix-none` on Linux to bypass locking. Safe because of the single-replica constraint. Windows native sqlite has no equivalent VFS, so the connection-string code branches on `OperatingSystem.IsLinux()`. |
| ACR `cr{token}` | Container image registry | Admin user enabled; password stored as a Container App secret. |
| ACS Email Service | Used by the legacy email-magic-link auth (Phase 5 removes this) | Will be removed when Phase 5 lands. |

### Required `azd env` variables

```powershell
# The MI to attach to the Container App. Required — empty value would silently strip
# the MI assignment on the next provision and break the FIC path even after the tenant
# policy is fixed.
azd env set USER_ASSIGNED_IDENTITY_RESOURCE_ID `
  "/subscriptions/.../resourceGroups/.../providers/Microsoft.ManagedIdentity/userAssignedIdentities/<name>"

# Optional. Empty string is the FIC path (currently broken — see §5).
# Set to a real value to use the client-secret fallback.
azd env set XEBIA_APP_CLIENT_SECRET "<secret-value>"
```

### Standard deploy

```powershell
azd up
```

This (re)provisions infra and deploys the latest container image. **Do not** provision without `USER_ASSIGNED_IDENTITY_RESOURCE_ID` set — Bicep treats it as required and will fail loudly rather than silently strip the MI.

---

## 7. Tenant configuration responsibilities

Things on the **Xebia tenant** side that the app cannot self-configure. Some are already done; the FIC item is the open blocker.

| Owner | Item | Status |
|---|---|---|
| Xebia tenant admin | Allow Azure CLI as a pre-authorized client for `access_as_user` (so devs can `az account get-access-token` for local testing without consent prompts) | Done |
| Xebia tenant admin | Add a Web platform redirect URI for each new dev environment (`https://localhost:5001/signin-oidc`) and for the prod FQDN | Done; per-developer URIs are added by each dev when setting up their Copilot Studio connector |
| Xebia tenant admin | Configure the federated credential on the app registration trusting the Marimer MI | Done — but inert because of the cross-tenant policy below |
| Xebia tenant admin | **Allow inbound workload identity federation from the Marimer tenant** (`da478866-394f-4ef4-8257-b720d51eaade`) under External Identities → Cross-tenant access settings | **Open** — fixing this lets us drop the client secret |
| Xebia tenant admin | Grant tenant-wide consent for `access_as_user` if you don't want each new agent user to see a consent prompt on first sign-in | Optional |
| Per-developer | Add their Copilot Studio connector's redirect URI to the app registration | One-time, per connector instance |
| Per-developer | Generate their own client secret on the app registration for their Copilot Studio connector | One-time, per connector instance — see [`copilot-studio-setup.md`](copilot-studio-setup.md) |

Things on the **Marimer tenant** side:

| Owner | Item | Status |
|---|---|---|
| Marimer admin | User-assigned MI exists in Marimer | Done (`2b9666a5-...`) |
| Marimer admin | MI attached to the Container App via Bicep | Done — `userAssignedIdentityResourceId` parameter on `main.bicep` |
| Marimer admin | Azure DNS / SSL on the Container App ingress FQDN | Default Container Apps wildcard cert; no custom domain configured |

---

## 8. Operational runbook

### "Sign-in stopped working in production"

Most likely the **client secret expired**. Check:
```powershell
az containerapp logs show -n ca-xsludqqyumyme -g rg-estimator-mcp --tail 100
```
Look for `AADSTS7000222` (secret expired) or similar. Generate a new client secret on the Xebia app registration, then:
```powershell
azd env set XEBIA_APP_CLIENT_SECRET "<new-value>"
azd up
```

### "Copilot Studio connector worked yesterday, now 401s"

Almost always the *connector's* secret expired (each developer's connector has its own — see §3). The fix is on the connector side: regenerate the secret on the Xebia app registration, paste the new value into the connector's OAuth config in Copilot Studio. Server-side state hasn't changed.

### "I want to verify the discovery handshake by hand"

```powershell
# PRM document — should return JSON with resource, authorization_servers, scopes_supported.
curl https://{fqdn}/.well-known/oauth-protected-resource/mcp

# Unauthenticated /mcp — should return 401 with WWW-Authenticate header
# pointing at the PRM URL.
curl -i https://{fqdn}/mcp

# Authenticated /api/catalog/export with a real Entra token — should return 200.
$token = (az account get-access-token --resource api://32c976ae-e874-4126-b2d8-10bc99ae9330 | ConvertFrom-Json).accessToken
curl -H "Authorization: Bearer $token" https://{fqdn}/api/catalog/export
```

If any of these fail, paste the JWT into [jwt.ms](https://jwt.ms) (it parses locally, never sends the token over the wire) and check `iss`, `aud`, `scp`. The four validations in §3 are the only things the server checks.

### "I need to rotate the production client secret"

1. Generate a new secret on the Xebia app registration (Certificates & secrets → New client secret).
2. `azd env set XEBIA_APP_CLIENT_SECRET "<new-value>"`
3. `azd up`
4. Verify sign-in works against the deployed URL.
5. Delete the old secret on the app registration.

### "I'm onboarding a new dev to the Copilot Studio integration"

Send them to [`copilot-studio-setup.md`](copilot-studio-setup.md). They need owner-level edit access on the Xebia app registration to add their redirect URI and generate their secret.

---

## 9. Known limits and follow-ups

- **No per-user authorization.** Any authenticated Xebia user can call any MCP tool. Per-role / per-group authorization is a planned follow-up; track in `plans/msal-auth.md` "Open questions / parking lot."
- **Copilot Studio "maker authentication" is the default.** Every agent user's calls appear under the maker's identity unless the connector is explicitly switched to end-user authentication. See `copilot-studio-setup.md` §"Authentication mode."
- **FIC blocker (AADSTS700236).** Tracked in §5. Re-enabling FIC removes a manual rotation responsibility but is not blocking any user-visible feature.
- **Single replica.** SQLite + Azure Files forces `maxReplicas=1`. If the catalog grows enough to need horizontal scale, the database needs to move (Azure SQL, Postgres, etc.) before the replica count can change.
- **Legacy email/Bearer-token path is still wired in.** Phase 5 of `plans/msal-auth.md` removes it. Until then, both schemes are accepted on `/mcp` and `/api/catalog/*`.

---

## 10. Where to look in the code

| What | File | Line(s) of interest |
|---|---|---|
| OIDC + JwtBearer registration | `src/EstimatorMcp.Web/Program.cs` | `AddMicrosoftIdentityWebApp(...)`, `AddMicrosoftIdentityWebApi(...)`, `AddPolicy("BearerOnly")` |
| `ForwardedHeaders` (must be early) | `Program.cs` | `app.UseForwardedHeaders()` near top of the pipeline |
| PRM-rewriting middleware (must be before `UseAuthentication`) | `Program.cs` | The anonymous `app.Use(async (ctx, next) => ...)` block |
| Status-code-pages exclusion for API paths | `Program.cs` | `app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/mcp") ...)` |
| PRM document | `Program.cs` | `app.MapGet("/.well-known/oauth-protected-resource/mcp", ...)` |
| Pinning `/mcp` to JWT-only | `Program.cs` | `app.MapMcp("/mcp").RequireAuthorization("BearerOnly")` |
| AzureAd config defaults | `src/EstimatorMcp.Web/appsettings.json` | `AzureAd` section |
| MI attachment + secret-fallback Bicep | `infra/resources.bicep` | `containerApp.identity`, the `secrets`/`env` `concat` blocks |
| `xebiaAppClientSecret` parameter plumbing | `infra/main.bicep` | parameter description explains the FIC blocker |
