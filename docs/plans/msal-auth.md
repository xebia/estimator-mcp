# MSAL / Entra Authentication Rework

**Branch:** `rocky/msal-auth` (off `main`)

## Goal

Replace the current email-magic-link + opaque-Bearer-token auth model with Entra ID (Microsoft.Identity.Web / MSAL) so that:

1. The Blazor UI does **SSO via Xebia tenant credentials** (interactive OIDC code flow).
2. The MCP endpoint is **OAuth-protected per the MCP spec** so an M365-hosted Copilot Studio agent can authenticate to it.

Both flows resolve against the **same Xebia app registration**. The app authenticates *itself* to Xebia Entra using a **federated assertion from the Marimer user-assigned managed identity** attached to the Container App — i.e. no client secret stored anywhere.

## Architecture summary

```
Blazor UI            Copilot Studio agent
    │                          │
    │ OIDC redirect            │ OAuth 2.1 (PRM-discovered)
    ▼                          ▼
Xebia Entra ←—— same app registration, same tenant
    │                          │
 cookie auth             JWT bearer auth
    │                          │
    ▼                          ▼
 Razor pages              /mcp endpoint
```

Two ASP.NET Core auth schemes side-by-side:
- **Cookies + OpenIdConnect** for Blazor pages — sets a session cookie after Xebia sign-in.
- **JwtBearer** for `/mcp` and `/api/catalog` — validates JWTs issued by Xebia Entra.

## What gets removed

Once the new flow is verified end-to-end:
- `Auth/BearerTokenAuthHandler.cs`
- `Components/Pages/Auth/Register.razor`, `Verify.razor`, `Token.razor`, `Tokens.razor`
- `Services/Auth/` — `AzureEmailService`, `IEmailService`, `VerificationService`, `IVerificationService`, `TokenService`, `ITokenService`, `TokenDisplayService`, `AzureEmailOptions`
- `User` and `ApiToken` EF entities + the migration that created them
- `AzureEmailService` config section in `appsettings*.json`

User has confirmed: breaking existing tokens is fine (sole user is the developer).

---

## Information needed before starting

These are filled in by Rocky. Stub values can be used in `appsettings.json` until the real ones are ready, but Phase 2 cannot complete without items 1–4.

- [x] **1. Xebia tenant ID** — `3d4d17ea-1ae4-4705-947e-51369c5a5f79`
  - Xebia portal → Entra ID → Overview → "Tenant ID"
- [x] **2. App registration client ID** — `32c976ae-e874-4126-b2d8-10bc99ae9330`
  - Xebia portal → Entra ID → App registrations → your app → "Application (client) ID"
- [x] **3. Application ID URI** — `api://32c976ae-e874-4126-b2d8-10bc99ae9330`
  - Same blade → "Expose an API" → "Application ID URI". If empty, set it to `api://{client-id}`.
- [x] **4. Exposed scope name** — suggest `access_as_user`
  - Same blade → "Expose an API" → "Add a scope". Admin/user consent both fine.
- [x] **5. Marimer MI client ID** — `2b9666a5-9d42-4d1d-a735-138d2ecc299c`
  - Marimer portal → the user-assigned managed identity resource → **Client ID** (NOT object ID).
  - This is the value Container Apps sets as `AZURE_CLIENT_ID` for Microsoft.Identity.Web's federated-credential flow.
- [x] **6. Container App FQDN** (optional for now) — `https://ca-xsludqqyumyme.prouddesert-77f66edd.centralus.azurecontainerapps.io`
  - Marimer portal → Container App → Overview → "Application Url".

---

## Phase 1 — Add packages, config shape, no behavior change

Goal: dependencies and config in place; old auth still fully working; new code unreachable. Safe to revert.

**Code (Claude):**
- [x] Add NuGet packages to `EstimatorMcp.Web.csproj`:
  - `Microsoft.Identity.Web` 4.9.0
  - `Microsoft.Identity.Web.UI` 4.9.0
  - ~~`Microsoft.AspNetCore.Authentication.JwtBearer`~~ — part of the ASP.NET Core shared framework on .NET 8+ and pulled transitively by `Microsoft.Identity.Web`; explicit reference unnecessary.
- [x] Add an `AzureAd` section to `appsettings.json` with `Instance`, `TenantId`, `ClientId`, `Domain`, `CallbackPath`, `SignedOutCallbackPath`, `Scopes`, `ClientCredentials` (federated MI assertion). Real values in place; section is unreferenced by any code yet.
- [x] Build passes (`dotnet build`).
- [x] Commit: `chore(auth): add Microsoft.Identity.Web packages and AzureAd config skeleton`.

**You (Rocky):**
- [ ] Confirm Phase 1 commit looks right before we proceed.

---

## Phase 2 — Wire OIDC for the Blazor UI

Goal: signing in to the Blazor UI redirects to Xebia, comes back, and shows the user as authenticated. `/mcp` and `/api/catalog` are still protected by the old Bearer handler at this point — we replace those in Phase 3.

**You (Rocky), before Claude can finish this phase:**
- [ ] Provide values 1–5 above (or paste them into `appsettings.Development.json` yourself).
- [ ] On the Xebia app registration → Authentication blade → add a **Web** platform with redirect URI `https://localhost:5001/signin-oidc` and front-channel logout URL `https://localhost:5001/signout-oidc`.
- [ ] On the Xebia app registration → Certificates & secrets → Federated credentials → confirm the credential pointing at the Marimer MI is present (this was already done with Troy, just verify).

**Code (Claude):**
- [x] In `Program.cs`, register Microsoft.Identity.Web with `AddMicrosoftIdentityWebApp`, configured for single-tenant. Reads `ClientCredentials` (federated MI assertion) from `AzureAd` config section.
- [x] Add `AddMicrosoftIdentityUI()` (via `AddControllersWithViews`) and `app.MapControllers()` so `/MicrosoftIdentity/Account/SignIn` and `/SignOut` routes work.
- [x] Add `<AuthorizeView>` to `MainLayout.razor` showing sign-in link / "Signed in as ..." with sign-out link. Add `Microsoft.AspNetCore.Components.Authorization` to `_Imports.razor`.
- [x] Add `BearerOnly` authorization policy and pin `/mcp` + `/api/catalog/*` to it so unauthenticated MCP/REST callers get 401, not an OIDC redirect.
- [x] Old `BearerTokenAuthHandler` still registered (parallel scheme). Old auth Razor pages still in place.
- [x] Build passes (`dotnet build`).
- [ ] ~~Manual smoke test~~ — Claude can't drive a browser; left for Rocky.
- [ ] Commit: `feat(auth): Blazor UI signs in via Xebia Entra (OIDC + federated MI)`.

**Heads-up — local dev needs an extra step:**
`SignedAssertionFromManagedIdentity` requires running on an Azure resource with the MI assigned. To smoke-test locally, the recommended path is a temporary client secret stored in user secrets (never committed), which overrides `AzureAd:ClientCredentials` for local runs only. See "Local-dev secret setup" below.

**You (Rocky):**
- [x] **Decide:** smoke test locally with a temporary client secret OR skip local test and validate after Phase 4 deploy? — chose local with secret in user secrets.
- [x] Smoke test: sign in, sign out, sign in again. Confirmed working — token endpoint returned 200, UI shows signed-in user.

### Local-dev secret setup (only if testing locally)

If you choose to test locally:

1. On the Xebia app registration → Certificates & secrets → "Client secrets" → "New client secret" — give it a short expiry (90 days). Copy the secret **value** immediately (it disappears after this view).
2. From the repo root, store it in user secrets so it never enters source control:
   ```powershell
   cd src/EstimatorMcp.Web
   dotnet user-secrets set "AzureAd:ClientCredentials:0:SourceType" "ClientSecret"
   dotnet user-secrets set "AzureAd:ClientCredentials:0:ClientSecret" "<paste-the-secret-value>"
   ```
   Setting index 0 of `ClientCredentials` overrides the production MI entry only for this dev environment. Microsoft.Identity.Web reads from environment + user-secrets + appsettings in that order.
3. Confirm with `dotnet user-secrets list`. Run `dotnet run --urls=https://localhost:5001` and try signing in.
4. (When done) Delete the client secret on the Xebia app registration; remove the user-secrets entries with `dotnet user-secrets remove`.

> Note: there's an unrelated pre-existing issue running locally on Windows native — the SQLite connection string uses `vfs=unix-none` which is Linux-only. If you hit this, the workaround is WSL or running in Docker. Not introduced by Phase 2.

---

## Phase 3 — Wire JwtBearer for `/mcp` and `/api/catalog`, plus PRM discovery

Goal: MCP and REST API accept Entra-issued JWTs. The MCP endpoint advertises Xebia as its authorization server per RFC 9728 so Copilot Studio can discover it. Old Bearer token still accepted in parallel until Phase 5 (gives us a fallback during testing).

**Code (Claude):**
- [x] Register JwtBearer via `AddMicrosoftIdentityWebApi(GetSection("AzureAd"))`. Authority/Audience derived from `TenantId` + new `Audience` field set to `api://{clientId}`. Standard MSAL token validation (issuer, audience, signature, lifetime).
- [x] `BearerOnly` policy now lists both `JwtBearer` and `BearerToken` schemes; either one succeeding satisfies the policy. The legacy scheme will be removed in Phase 5.
- [x] Map `GET /.well-known/oauth-protected-resource/mcp` (anonymous) returning:
  ```json
  {
    "resource": "https://{host}/mcp",
    "authorization_servers": ["https://login.microsoftonline.com/{xebia-tenant-id}/v2.0"],
    "scopes_supported": ["api://{client-id}/access_as_user"],
    "bearer_methods_supported": ["header"]
  }
  ```
- [x] Middleware sets `WWW-Authenticate: Bearer resource_metadata="..."` on 401 responses to `/mcp/*`. Must wrap the auth pipeline (registered before `UseAuthentication`) so the unwind path runs through it after authz short-circuits with 401.
- [x] Build passes (`dotnet build`).
- [x] All three smoke tests pass — PRM document correct, WWW-Authenticate header points at PRM doc on unauth /mcp, JWT-protected REST returns 200 with valid Entra token.
- [x] Commit: `feat(auth): JWT bearer + PRM discovery on /mcp and /api/catalog` (followed by middleware-order fix commit).

**You (Rocky):**
- [x] Pre-authorize Azure CLI (`04b07795-8ddb-461a-bbee-02f9e1bf7b46`) for the `access_as_user` scope on the Xebia app registration's "Expose an API" → "Authorized client applications" blade. Required for `az` to acquire tokens against this resource without interactive consent prompts.
- [x] Verify the PRM document:
  ```powershell
  curl -k https://localhost:5001/.well-known/oauth-protected-resource/mcp
  ```
  Should return a JSON document with `resource`, `authorization_servers`, `scopes_supported`.
- [x] Verify the WWW-Authenticate header on an unauthenticated /mcp request — PASS (`Bearer resource_metadata="https://localhost:5001/.well-known/oauth-protected-resource/mcp"`).
- [x] Get a JWT via `az` and call the REST API — PASS (200, full catalog returned, token validated with aud `api://32c976ae-...`, iss `sts.windows.net/3d4d17ea-.../`, scp `access_as_user`, upn `Rocky.Lhotka@xebia.com`).
- [ ] (Optional) MCP Inspector dry-run pointed at `https://localhost:5001/mcp` with the OAuth scope `api://32c976ae-e874-4126-b2d8-10bc99ae9330/access_as_user`. Skipped — Phase 4 deploy + real Copilot Studio call is the more meaningful validation.

---

## Phase 4 — Connect Copilot Studio agent

Goal: a real Copilot Studio agent in Xebia M365 calls `/mcp` successfully. This is the validation that the OAuth wiring is right end-to-end. Mostly your work — Claude is on standby for issues.

**You (Rocky):**
- [x] Deploy the Phase 3 build to the Container App (`azd up` after setting Bicep params via `azd env config set`).
- [x] Add `https://{fqdn}/signin-oidc` to the Xebia app registration redirect URIs.
- [x] Browser sign-in test against the deployed URL — confirmed working (logs show 200 on token endpoint, no exceptions).
- [ ] In Copilot Studio: register the MCP server, point it at `https://ca-xsludqqyumyme.prouddesert-77f66edd.centralus.azurecontainerapps.io/mcp`, supply the OAuth scope `api://32c976ae-e874-4126-b2d8-10bc99ae9330/access_as_user`.
- [ ] Trigger a tool call from the agent. Confirm 200s in the app logs.

**Code (Claude), already addressed during Phase 4 deploy:**
- [x] `UseForwardedHeaders` middleware so PRM `resource` and OIDC redirect URLs are emitted as `https://` (Container Apps' ingress terminates TLS).
- [x] Restricted `UseStatusCodePagesWithReExecute` to non-API paths so /mcp 401s have empty bodies (not the Blazor index HTML).
- [x] Bicep updated to attach the user-assigned MI to the Container App and to support an optional `xebiaAppClientSecret` for the secret-fallback path.

**Open issue — FIC blocked, secret fallback in use:**
The federated managed-identity flow that Troy walked through fails with **AADSTS700236**: *"Entra ID tokens issued by issuer `…/da478866-…/v2.0` may not be used for federated identity credential flows for applications or managed identities registered in this tenant."* The Xebia tenant has a cross-tenant access policy blocking inbound workload identity federation from external Entra tenants. Until that's resolved (Xebia admin work via Entra → External Identities → Cross-tenant access settings), the deployed app uses a client secret stored as the `azure-ad-client-secret` Container App secret, with env vars overriding `AzureAd:ClientCredentials[0]:SourceType` → `ClientSecret`. The MI assignment stays attached so re-enabling FIC later requires only unsetting the env vars.

- [ ] (Future) Get Troy / Xebia admin to allow inbound workload identity federation from Marimer tenant `da478866-394f-4ef4-8257-b720d51eaade`. Then `azd env config unset infra.parameters.xebiaAppClientSecret` + `azd up` switches back to FIC.

---

## Phase 5 — Remove the old auth model

Goal: rip out the email-magic-link / Bearer-token system once the new flow is proven. Single commit so it's easy to revert if Phase 4 turned up something we missed.

**Code (Claude):**
- [ ] Delete `Auth/BearerTokenAuthHandler.cs`.
- [ ] Delete `Components/Pages/Auth/{Register,Verify,Token,Tokens}.razor`.
- [ ] Delete `Services/Auth/` (Email, Verification, Token services + interfaces + options).
- [ ] Remove `User` and `ApiToken` EF entities from `Data/Entities.cs` and `AppDbContext`.
- [ ] Generate an EF migration that drops the `Users` and `ApiTokens` tables; verify it applies cleanly against a copy of the prod DB.
- [ ] Remove `AzureEmailService` config from `appsettings.json` and `appsettings.Development.json`.
- [ ] Remove all `AddScoped<IEmailService,...>` etc. from `Program.cs`.
- [ ] Remove the parallel BearerToken scheme from the auth policy added in Phase 3.
- [ ] Update `CatalogCli` to fetch tokens via `DefaultAzureCredential` (or `InteractiveBrowserCredential`) for the new `api://{client-id}/.default` scope, and remove the `--token` flag plumbing.
- [ ] `dotnet build && dotnet test` passes.
- [ ] Commit: `chore(auth): remove email/Bearer-token auth model`.

**You (Rocky):**
- [ ] Final smoke test in prod: Blazor UI sign-in works, Copilot Studio agent works, CatalogCli works.
- [ ] Merge `rocky/msal-auth` to `main` via PR.

---

## Open questions / parking lot

- [ ] Authorization model beyond authentication: do all signed-in Xebia users get full access, or do we want group/role checks (e.g. only members of a specific Entra security group can write to the catalog)? Punt to a follow-up phase unless you say otherwise.
- [ ] CatalogCli end-user experience: device-code flow vs. interactive-browser vs. CLI-driven `az` token fetch. Default plan above uses `DefaultAzureCredential` which tries them all.
- [ ] Whether the `/api/catalog` endpoints should *also* accept the Blazor cookie for browser-driven admin actions, or stay JWT-only.
