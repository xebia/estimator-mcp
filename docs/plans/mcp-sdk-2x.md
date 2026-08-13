# Plan: ModelContextProtocol.AspNetCore 1.4.1 → 2.x

**Status:** Phase 1 implemented. Phases 2–4 outstanding.

Dependabot proposed `ModelContextProtocol.AspNetCore` 1.4.1 → 2.1.0. Major-version bumps of `ModelContextProtocol*` are suppressed in `.github/dependabot.yml`, so this is a deliberate piece of work rather than an auto-merge.

---

## 1. What this migration actually is

**It is not a code migration.** 2.1.0 compiles against the existing code with **0 errors and 0 warnings**, and all tests pass unchanged. None of the SDK's deprecation diagnostics fire, because this server does not use Roots, Sampling, Logging, or `AuthorizationRedirectDelegate`.

**It is a runtime-behaviour migration.** A green build is the least informative signal here — everything that can change, changes on the wire. So the upgrade was validated by running 1.4.1 and 2.1.0 locally with authorization removed and diffing actual MCP traffic (`initialize`, `tools/list`, `tools/call`).

### Measured wire diff

| | 1.4.1 | 2.1.0 (defaults) | 2.1.0 + `Stateless=false` |
|---|---|---|---|
| `initialize` status | 200 | 200 | 200 |
| `Mcp-Session-Id` issued | yes | **no** | yes |
| Negotiated protocol | 2025-06-18 | 2025-06-18 | 2025-06-18 |
| Tool count | 6 | 6 | 6 |
| Tool names | unchanged | unchanged | unchanged |
| `tools/list` item keys | `description, execution, inputSchema, name` | **`description, inputSchema, name`** | **`description, inputSchema, name`** |
| `tools/call` result | `content[0].type = text` | same | same |
| `tools/call` payload | baseline | byte-identical | byte-identical |

Two behavioural changes, both explained by the 2.0.0 release notes:

1. **`HttpServerTransportOptions.Stateless` now defaults to `true`**, so no session is issued.
2. **`execution` disappears from `tools/list` entries**, consistent with Tasks moving to the separate `ModelContextProtocol.Extensions.Tasks` package.

Notably **tool names and call results are unchanged**. The 2.0.0 breaking change "non-object tool results emit raw instead of wrapped" does not affect this server: every tool returns `string`, which the SDK still wraps as a text content block.

---

## 2. Deployment constraint: no canary

The usual "deploy a revision at 0 % traffic, test it, then shift" play **is not safe here**.

`infra/resources.bicep` runs the app in single-revision mode with `maxReplicas: 1`, and the SQLite connection string uses `vfs=unix-none`, which **disables file locking**. That is safe only because exactly one writer is guaranteed. A second concurrent revision against the same Azure Files volume is a second writer, and risks database corruption.

Rollout therefore relies on **revision rollback**, not parallel validation:

```bash
az containerapp revision list -n <app> -g <rg> -o table   # note the active revision first
az containerapp revision activate -n <app> -g <rg> --revision <previous>   # rollback path
```

If parallel validation is ever wanted, it needs a separate container app with its own database, not a second revision of this one.

---

## 3. Phases

### Phase 1 — Upgrade with behaviour pinned ✅ implemented

- `ModelContextProtocol.AspNetCore` → 2.1.0 in `Directory.Packages.props`
- `WithHttpTransport(options => options.Stateless = false)` in `Program.cs`
- `VersionPrefix` → 0.2.0

`Stateless` is pinned deliberately. Upgrading the SDK and changing session semantics are two separate decisions, and this change makes only one of them. It reduces the post-upgrade wire delta to a single field.

**Verified:** 0 warnings, 30/30 tests, and the wire diff against the 1.4.1 baseline reduced to exactly one difference — the dropped `execution` field.

### Phase 2 — Confirm the consumer ⛔ blocks deployment

The one thing that cannot be verified from this repo: whether the **Copilot Studio connector** cares about either remaining change. Calling `/mcp` for real requires an Entra token, and the agent side cannot be exercised locally.

Check, against a 2.1.0 instance:

1. The agent completes OAuth and calls a tool successfully.
2. Tool discovery still lists all six tools.
3. An estimate round-trip (`get_catalog_features` → `calculate_estimate`) returns the same numbers as before.

The lowest-risk way to do this before deploying is to expose a local 2.1.0 instance through a tunnel and point a **test** agent at it.

### Phase 3 — Deploy

Note the active revision, `azd deploy`, re-run the Phase 2 checks against production, and roll back to the noted revision if anything misbehaves.

### Phase 4 — Follow-ups, each on its own

- **Adopt stateless** (`Stateless = true`) once 2.1.0 is proven. With `maxReplicas: 1` there is no scale-out pressure, so this is cleanup rather than urgency — but it aligns with the SDK default and removes server-side session state.
- **Evaluate 2.x's built-in OAuth / protected-resource support** against the hand-rolled middleware in `Program.cs` (the 401 `WWW-Authenticate` rewrite and the manual `/.well-known/oauth-protected-resource/mcp` endpoint). If the SDK now covers RFC 9728 properly, roughly 30 lines of custom code can go.
- **Remove the `ModelContextProtocol*` ignore block** from `.github/dependabot.yml` so future 2.x updates flow normally.

---

## 4. Version call

Per [`VERSIONING.md`](../../VERSIONING.md) this is a **MINOR** bump — **0.2.0** — not a major one.

A dependency's major version does not imply ours. The versioned contract is the MCP tool surface, and tool names, parameters, and call results are all unchanged.

The one honest wrinkle: `execution` disappears from `tools/list`, and the policy lists "removing a field from a tool's response" as major. That field is SDK-emitted protocol metadata about task execution, not part of this server's contract, so it is treated as minor — recorded here rather than left as folklore, since it is a judgement call rather than a mechanical reading of the rule.
