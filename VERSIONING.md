# Versioning

This project follows [Semantic Versioning 2.0.0](https://semver.org/): `MAJOR.MINOR.PATCH`.

**Current version:** see `VersionPrefix` in [`Directory.Build.props`](Directory.Build.props) — the single hand-edited source of truth.

## What the version describes

The version describes **the MCP tool surface at `/mcp`** — the contract external consumers depend on, such as the Copilot Studio agent.

It deliberately does **not** describe the catalog contents. Adding a feature to the catalog, adjusting a role's hours, or changing a Copilot multiplier will change the numbers `CalculateEstimate` returns without being an API change. Catalog data carries its own versioning through the timestamped `catalog-{ISO8601}.json` seed files and the edit history in the database. If you are hunting an estimate that changed, look at the catalog, not the app version.

## What counts as what

### MAJOR — consumers must change something

- Removing or renaming an MCP tool
- Removing or renaming a tool parameter, or making an optional one required
- Changing the meaning of an existing parameter (e.g. redefining what a size string means)
- Removing a field from a tool's response, or changing a field's type
- Changing the estimate **formula** such that identical input against an identical catalog yields different numbers
- Requiring a new OAuth scope, or otherwise changing what a caller must present to authenticate

### MINOR — additive, existing callers keep working

- Adding a new MCP tool
- Adding an optional parameter to an existing tool
- Adding a field to a response
- Broadening what an existing parameter accepts
- New Blazor UI features, new CLI commands

### PATCH — no contract change

- Bug fixes that do not change response shape
- Performance, logging, and infrastructure work
- Dependency updates, including security bumps
- Documentation

## Pre-1.0

While the version is `0.x`, semver grants no compatibility promise, and a breaking change may land in a minor bump. The categories above are still applied and still recorded in release notes, so the history stays honest and the move to `1.0.0` is a formality rather than an archaeology exercise.

Ship `1.0.0` when the tool surface is considered stable enough that breaking it warrants a major bump.

## Releasing

1. Bump `VersionPrefix` in `Directory.Build.props` in a PR, and land it on `main`.
2. Tag the merge commit and push:
   ```bash
   git tag v0.2.0
   git push origin v0.2.0
   ```
3. The `release` workflow verifies the tag matches `VersionPrefix`, builds, tests, and publishes a GitHub Release with generated notes.

A tag that disagrees with `Directory.Build.props` fails the workflow rather than publishing a mislabelled release. Prerelease tags (`v0.2.0-rc.1`) build with the suffix applied and are marked as prereleases.

## Where the version shows up

Every assembly is stamped from `VersionPrefix`. CI additionally passes the commit SHA, so `InformationalVersion` reads `0.1.0+<sha>` and any deployed build traces back to an exact commit.

Consumers can read it at runtime through the **`GetServerVersion`** MCP tool, which reports the server version and commit alongside the catalog's schema version and timestamp:

```json
{
  "server":  { "version": "0.1.0+abc1234", "semanticVersion": "0.1.0", "commit": "abc1234" },
  "catalog": { "schemaVersion": "2.0", "timestamp": "2026-03-01T12:00:00Z", "featureCount": 57, "error": null }
}
```

Both halves matter. If an agent sees an estimate change, the server version tells it whether the tool surface or formula moved, and the catalog timestamp tells it whether the underlying data did — the two change independently.
