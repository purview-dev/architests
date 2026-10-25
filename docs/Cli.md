# Command line

`architests` runs the parts of the policy that need no build: the schema check, the policy's own
integrity rules, and the waiver review. The same logic backs the TUnit rules, so the tool and the
test suite cannot disagree.

## Install

```bash
dotnet tool install --global Purview.Architecture.Testing.Cli
```

## Commands

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

Both commands accept `--repository <path>` (or `-r`) to name the repository root. Without it, the
tool walks up from the current directory to the folder holding `.config/architecture-policy.json`,
and stops at the repository boundary rather than reading a parent folder's policy.

### `check`

```bash
architests check
architests check --format sarif > architests.sarif
```

Runs **every rule** against the repository and reports what the test suite would. This is the
command for a repository that has no test project, or for a pipeline step that should fail before
the tests run.

It needs a build, because the type-level, API and port rules read compiled assemblies. Rules whose
policy group is off are skipped, exactly as they are under TUnit.

| Format | What it is for |
| -- | -- |
| `text` | Reading in a terminal: one `ruleId: message` line per violation. |
| `json` | Another tool. The violations as an array of `rule`, `project` and `message`. |
| `sarif` | GitHub code scanning, an IDE, or a pull-request annotation. |

The SARIF output carries each rule's title and description from the rule catalogue, and points a
finding at the offending project's `.csproj`. There is no line number: the evaluator reads project
references and compiled metadata, not source, so the project file is the most precise location it
can honestly give.

```yaml
- name: Architecture
  run: architests check --format sarif > architests.sarif
  continue-on-error: true

- uses: github/codeql-action/upload-sarif@v4
  with:
    sarif_file: architests.sarif
```

`continue-on-error` is deliberate there: the upload step should still run when the check fails,
which is exactly when there is something to upload.

### `validate`

```bash
architests validate
```

1. Validates the policy against the schema embedded in the package.
2. Checks the policy's own consistency: unknown responsibility names, a dependency that is both
   permitted and forbidden, duplicate names or aliases, and — when `policyIntegrity.checkReachability`
   is on — a prohibition the permitted dependencies can bypass.

It needs no build and no project graph, which makes it the right check for a pre-commit hook or a
fast CI job.

### `api capture` and `api check`

```bash
architests api capture
architests api check
```

`capture` writes the committed baseline from the compiled assemblies. `check` compares the build
with it and exits `1` on any added, removed or changed public API. The document is canonical, so an
unchanged API regenerates byte for byte.

The commands scan the repository's own build output, even where `assemblyRules.assemblyRoots` is
empty (the test rule treats that as the test output directory). Build the solution first, and pass
`--baseline <path>` to use a baseline other than the policy's `apiRules.baseline`.

`check --format json` prints the differences as JSON, and `--format sarif` prints a SARIF 2.1.0
document for a code-scanning annotation step. A removal is reported at `error` level; an addition or a
shape change is a `warning`.

### `graph`

```bash
architests graph
architests graph --format mermaid
```

Renders the project graph from the policy's `sourceRoots`. The default text output lists each project
with its responsibility and category, then its references. `--format mermaid` emits a flowchart for a
pull request or the documentation site.

### `waivers list`

```bash
architests waivers list
architests waivers list --stale
```

Lists each waiver with its rules and review state. `--stale` shows only the waivers that are due
under `waiverPolicy.maxAgeDays`, and exits non-zero when any is, so it works as a gate.

A waiver is stale when it is older than the maximum, has no `reviewedOn` while a maximum is
configured, carries a date that is not `yyyy-MM-dd`, or claims a review in the future.

## Exit codes

| Code | Meaning |
| -- | -- |
| `0` | The check passed. |
| `1` | Findings: the policy is invalid, inconsistent, or a stale waiver was requested. |
| `2` | A malformed command line: unknown command, unknown option, or a missing option value. |

The split matters in CI: `1` is a policy problem to fix, `2` is a broken invocation to fix.

## CI example

```yaml
- name: Validate the architecture policy
  run: |
    dotnet tool install --global Purview.Architecture.Testing.Cli
    architests validate
    architests waivers list --stale
```
