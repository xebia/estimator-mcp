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
- [ ] In `Program.cs`, register Microsoft.Identity.Web with `AddMicrosoftIdentityWebApp`, configured for single-tenant with `ClientCredentials = SignedAssertionFromManagedIdentity` (Marimer MI client ID).
- [ ] Add `AddMicrosoftIdentityUI()` to Razor Components for sign-in/sign-out endpoints.
- [ ] Add a sign-in / sign-out / "signed in as" affordance to the Blazor layout.
- [ ] Keep the old `BearerTokenAuthHandler` registered so `/mcp` and `/api/catalog` still work.
- [ ] Build passes; manual smoke test: `dotnet run`, browse to `https://localhost:5001`, sign in with a Xebia account, see the email surface in the UI.
- [ ] Commit: `feat(auth): Blazor UI signs in via Xebia Entra (OIDC + federated MI)`.

**You (Rocky):**
- [ ] Smoke test: sign in, sign out, sign in again. Confirm it works.

---

## Phase 3 — Wire JwtBearer for `/mcp` and `/api/catalog`, plus PRM discovery

Goal: MCP and REST API accept Entra-issued JWTs. The MCP endpoint advertises Xebia as its authorization server per RFC 9728 so Copilot Studio can discover it. Old Bearer token still accepted in parallel until Phase 5 (gives us a fallback during testing).

**Code (Claude):**
- [ ] Register a JwtBearer scheme alongside the cookie/OIDC scheme:
  - Authority = `https://login.microsoftonline.com/{xebia-tenant-id}/v2.0`
  - Audience = `api://{client-id}` (or just `{client-id}` — both are commonly accepted)
  - Token validation parameters: validate issuer, audience, signature, lifetime.
- [ ] Compose authentication policies so:
  - Blazor pages → cookie scheme.
  - `/mcp` and `/api/catalog/*` → JwtBearer scheme **and** existing BearerToken scheme during transition.
- [ ] Map a `/.well-known/oauth-protected-resource` endpoint that returns:
  ```json
  {
    "resource": "https://{host}/mcp",
    "authorization_servers": ["https://login.microsoftonline.com/{xebia-tenant-id}/v2.0"],
    "scopes_supported": ["api://{client-id}/access_as_user"],
    "bearer_methods_supported": ["header"]
  }
  ```
- [ ] Make `MapMcp("/mcp")` send a `WWW-Authenticate: Bearer resource_metadata="..."` header on 401 (per the MCP spec) so clients know where to find the PRM document.
- [ ] Build passes; manual smoke test:
  - `az account get-access-token --resource api://{client-id} --tenant {xebia-tenant-id}` → grab a JWT.
  - `curl -H "Authorization: Bearer <jwt>" https://localhost:5001/api/catalog/export` → 200.
  - Same JWT against `/mcp` via MCP Inspector → tools list.
- [ ] Commit: `feat(auth): JWT bearer + PRM discovery on /mcp and /api/catalog`.

**You (Rocky):**
- [ ] Run the `az account get-access-token` smoke test and confirm a 200.
- [ ] (Optional) MCP Inspector dry-run.

---

## Phase 4 — Connect Copilot Studio agent

Goal: a real Copilot Studio agent in Xebia M365 calls `/mcp` successfully. This is the validation that the OAuth wiring is right end-to-end. Mostly your work — Claude is on standby for issues.

**You (Rocky):**
- [ ] Deploy the Phase 3 build to the Container App (so Copilot Studio can reach a public URL).
- [ ] Add `https://{fqdn}/signin-oidc` to the Xebia app registration redirect URIs.
- [ ] In Copilot Studio: register the MCP server, point it at `https://{fqdn}/mcp`, supply the OAuth scope `api://{client-id}/access_as_user`.
- [ ] Trigger a tool call from the agent. Confirm 200s in the app logs.

**Code (Claude), only if issues surface:**
- [ ] Diagnose any 401s / discovery problems from logs and adjust configuration. Common things to check: PRM `resource` exact-match, scope name, Application ID URI consistency, audience format, redirect URI exact-match.

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
