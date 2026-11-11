# Contract: Release Pipeline

Workflow `.github/workflows/release.yml`. It's triggered by pushing tag `v{DnuiRuntimeVersion}`
(publish) or by `workflow_dispatch` (dry run).

## Jobs

| Job | Runs when | Does |
|---|---|---|
| `pack` | always | 1. Tag check (tags only): `GITHUB_REF_NAME == v$(DnuiRuntimeVersion)`, else fail before packing. 2. Pack the 6 runtime packages, `Cmdless.Dnui` (+ `.snupkg`) and `Cmdless.Dnui.Tool` (+ `.snupkg`). 3. Fail if any `.nupkg` is over 250 MB, and print every size. 4. Upload the packages as an artifact. |
| `publish` | tag only, after `pack`; environment `nuget` (required reviewers) | 1. `NuGet/login` (OIDC). 2. Push the 6 runtime packages with `--skip-duplicate`. 3. Wait until all 6 return 200 from the flat container (poll every 30 s, at most 30 min). 4. `NuGet/login` again. 5. Push `Cmdless.Dnui` and `Cmdless.Dnui.Tool` with `--skip-duplicate` (symbols go up with them). |
| `github-release` | tag only, after `publish` | Create the release `v{version}` with the packages attached. If it already exists, upload with `--clobber`. |

## Permissions and configuration

| Item | Value |
|---|---|
| `pack` | `contents: read` |
| `publish` | `id-token: write`, `contents: read`, `environment: nuget` |
| `github-release` | `contents: write` |
| Secret | `NUGET_USER`: the nuget.org profile name for `NuGet/login` (not a credential; a secret just keeps it out of logs) |
| Credentials | none stored; the API key is minted per run through OIDC |

## Manual setup (maintainer, once)

1. nuget.org: create the organization `cmdless`.
2. nuget.org → the `cmdless` org → Trusted Publishing: add a GitHub Actions policy with owner
   `cmdless`, repository `dnui`, workflow `release.yml` and environment `nuget`. *(done)*
3. GitHub → `cmdless/dnui` → Settings → Environments: create `nuget` with yourself as a required
   reviewer, and limit it to `v*` tags. *(done)*
4. GitHub → Settings → Secrets: set `NUGET_USER`. *(done)*
5. Optional: email `account@nuget.org` to reserve the `Cmdless.` prefix.

## Guarantees

- Before `publish` is approved, nothing is pushed.
- The library and the tool are never on nuget.org without all six runtime packages of the same
  version being downloadable.
- Re-running any job is safe: existing packages are skipped, and an existing release is updated.
- Prerelease tags (`v0.1.0-preview.1`) behave the same way.
