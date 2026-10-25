# Purview.Architecture.Testing.Cli

The `architests` command-line tool: run the parts of a
[Purview.Architecture.Testing](https://github.com/purview-dev/architests) policy that need no test
run or build.

## Install

```bash
dotnet tool install --global Purview.Architecture.Testing.Cli
```

## Use

```text
architests check [--format text|json|sarif]
architests validate                     # schema + policyIntegrity
architests api capture                  # write .config/api-surface.json
architests api check [--format text|json|sarif]
architests waivers list [--stale]       # the policy's waivers and their review state
architests graph [--format text|mermaid]
architests --help
architests --version
```

Every command accepts `--repository <path>` (or `-r`). Without it, the tool walks up from the
current directory to the folder holding `.config/architecture-policy.json`, and stops at the
repository boundary rather than reading a parent folder's policy.

### `check`

Runs **every rule** against the repository, reporting what the test suite would — for a repository
with no test project, or a pipeline step that should fail before the tests run. It needs a build,
because the type-level, API and port rules read compiled assemblies.

`--format sarif` emits SARIF 2.1.0 carrying each rule's title and description, with findings
pointed at the offending `.csproj`, so violations appear as annotations in a pull request.

### Exit codes

| Code | Meaning |
| -- | -- |
| `0` | The check passed. |
| `1` | Findings: the policy is invalid, inconsistent, or a stale waiver was requested. |
| `2` | A malformed command line: unknown command, unknown option, or a missing option value. |

### `validate`

Validates the policy against the schema embedded in the package and runs `policyIntegrity`, which
reports unknown responsibility names, a dependency both permitted and forbidden, duplicate names or
aliases, and — when `checkReachability` is on — a prohibition the permitted dependencies can bypass.
It needs no build, so it suits a pre-commit hook or a fast CI job.

### `api capture` and `api check`

`capture` writes the public API baseline from the compiled assemblies; `check` compares the build
with it and exits `1` on any difference. The document is canonical, so an unchanged API regenerates
byte for byte. Both scan the repository's build output, so build first.

### `graph`

`graph` renders the project graph from the policy's `sourceRoots`; `--format mermaid` emits a
flowchart, and the default text output lists each project's responsibility and references.

### `waivers list`

Lists each waiver with its rules and review state. `--stale` shows only waivers due under
`waiverPolicy.maxAgeDays`, and exits `1` when any is.

## Documentation

- [Command line](https://github.com/purview-dev/architests/blob/main/docs/Cli.md)
- [Policy reference](https://github.com/purview-dev/architests/blob/main/docs/Policy-Reference.md)

---

Part of [Purview.BuildSdk](https://github.com/purview-dev/build-sdk) · [purview.dev](https://purview.dev/projects/architests/)
