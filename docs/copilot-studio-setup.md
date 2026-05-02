# Connecting a Copilot Studio agent to the Estimator MCP server

This guide is for Xebia developers who want their own Copilot Studio agent to call this MCP server. Each developer goes through it once for their agent.

## What you need

- A Microsoft Account with access to the Xebia tenant (`3d4d17ea-1ae4-4705-947e-51369c5a5f79`).
- Edit access to the **Estimator MCP** app registration in Xebia Entra ID — specifically the ability to add redirect URIs and generate client secrets. If you don't have it, ask the team lead for the app registration owner role.
- An agent in Copilot Studio that you can edit.

## What you'll end up with

- A new client secret on the shared app registration, dedicated to *your* Copilot Studio connector.
- An additional redirect URI on the same app registration, pointing at *your* Copilot Studio environment.
- An MCP connection in your Copilot Studio agent that authenticates using your Xebia identity (or the agent users' identities, depending on how you configure the connection).

## Step 1 — Add the MCP tool to your agent

1. Open `https://copilotstudio.microsoft.com`, edit your agent.
2. Tools → Add a tool → look for **Model Context Protocol** / **MCP server** (the exact label changes between Copilot Studio releases).
3. **Server URL:** `https://ca-xsludqqyumyme.prouddesert-77f66edd.centralus.azurecontainerapps.io/mcp`
4. The connection dialog will appear. Pick **OAuth 2.0** as the authentication type.

## Step 2 — Switch to manual OAuth configuration

Copilot Studio's default flow tries Dynamic Client Registration (RFC 7591) against the authorization server. **Entra doesn't support DCR**, so the auto path fails with `GetDynamicClientRegistrationResultAsync failed. Status Code: NotFound`. Skip it:

1. Look for a **Manual** option (radio button / dropdown / "advanced settings" expander) on the OAuth setup. Pick it.
2. The dialog will now show fields for Client ID, Client Secret, Authorization URL, etc.
3. **Take note of the Redirect URL value Copilot Studio shows you.** It looks something like
   `https://global.consent.azure-apim.net/redirect/<long-id>`
   and is unique to your Copilot Studio environment + connector.

## Step 3 — Register your redirect URI on the Xebia app registration

This step **must happen before you click "Sign in"** in the Copilot Studio dialog, otherwise you'll get `AADSTS50011: redirect URI mismatch`.

1. Xebia portal → Microsoft Entra ID → App registrations → **Estimator MCP** (client ID `32c976ae-e874-4126-b2d8-10bc99ae9330`).
2. **Authentication** blade → under **Web** platform → **Add URI**.
3. Paste the redirect URL you copied from Copilot Studio in Step 2 — exactly as displayed, no edits, no trailing slash.
4. Save.

Entra is byte-for-byte strict about this — encoding, casing, and trailing characters all matter.

## Step 4 — Generate a client secret for your connector

Don't reuse another developer's secret. Each connector should hold its own so secrets can be rotated independently.

1. Same app registration → **Certificates & secrets** → **New client secret**.
2. Description: `Copilot Studio connector — <your-name>` (so we know which secret belongs to which connector when rotating).
3. Expiry: 180 days.
4. Click **Add**, then **immediately copy the Value column**. The secret is only displayed once.

## Step 5 — Fill in the Copilot Studio OAuth fields

| Field | Value |
|---|---|
| **Client ID** | `32c976ae-e874-4126-b2d8-10bc99ae9330` |
| **Client Secret** | the secret value you generated in Step 4 |
| **Authorization URL** | `https://login.microsoftonline.com/3d4d17ea-1ae4-4705-947e-51369c5a5f79/oauth2/v2.0/authorize` |
| **Token URL Template** | `https://login.microsoftonline.com/3d4d17ea-1ae4-4705-947e-51369c5a5f79/oauth2/v2.0/token` |
| **Refresh URL** | same as the Token URL |
| **Scopes** | `api://32c976ae-e874-4126-b2d8-10bc99ae9330/access_as_user offline_access` (space-separated; `offline_access` is what gets you a refresh token so the connection doesn't expire after an hour) |
| **Redirect URL** | the value Copilot Studio displayed (which you also registered in Step 3) |

Save the connector.

## Step 6 — Create the connection and sign in

After saving the connector, Copilot Studio will let you create a connection. Click it, and you'll be redirected to a Microsoft sign-in page.

- Sign in with your Xebia credentials.
- Consent to `access_as_user` on first sign-in.
- You should land back in Copilot Studio with the connection showing as healthy/green.

## Step 7 — Verify with a tool call

Trigger a conversation with your agent that should fan out to one of the MCP tools — e.g. *"List the available features in the catalog"*. The agent should call `GetCatalogFeatures` and return JSON.

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `GetDynamicClientRegistrationResultAsync failed. NotFound` | You're still on the auto/DCR path | Switch to Manual (Step 2) |
| `AADSTS50011: redirect URI mismatch` | The redirect URI you entered in Copilot Studio doesn't match what's registered | Step 3 — copy the URL exactly, no edits |
| `AADSTS65001: consent required` | The first user signing in needs to consent | Click Accept on the consent screen, or have an admin grant tenant-wide consent on the app registration's API permissions |
| `AADSTS70011: invalid scope` | Typo in the scope string | Step 5 — must be `api://32c976ae-…/access_as_user offline_access` exactly |
| `401 from /mcp after sign-in succeeds` | Token has the wrong audience or scope | Check the JWT (paste at jwt.ms — local-only, never sends the token over the wire). Audience should be `api://32c976ae-…`, `scp` claim should include `access_as_user` |
| `403 / insufficient permissions` | The user signed in but isn't authorized | Currently every Xebia user is authorized. If you see this, surface the issue — there may be a policy change pending |

For other errors, paste the message and the output of:
```powershell
az containerapp logs show -n ca-xsludqqyumyme -g rg-estimator-mcp --tail 50
```

…in the team channel.

## Authentication mode (whose credentials are used)

By default, Copilot Studio connections use **maker authentication** — every agent user's calls go through *your* stored credentials, and the MCP server sees your `upn` for all of them. This is fine for early development but bad for audit and per-user authorization.

To switch to **end-user authentication** (each agent user signs in with their own Xebia credentials), look in your agent's tool/connection settings for an option labeled like "Authentication", "End-user authentication", or "Sign-in for users". The exact location moves between Copilot Studio releases; if you can't find it, ask in the team channel.

## What the MCP server validates today

- Token issuer = Xebia tenant (`https://login.microsoftonline.com/3d4d17ea-…/v2.0` or the v1 `sts.windows.net/…/` form).
- Token audience = `api://32c976ae-…` or `32c976ae-…`.
- Token signature, lifetime, and `access_as_user` scope.

There is **no per-role or per-user authorization** yet — any authenticated Xebia user can call any MCP tool. That's a planned follow-up. Don't rely on the absence of authorization checks for production use cases.
