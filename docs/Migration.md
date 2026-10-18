# Migration and the dependency budget

A policy that describes the architecture you wish you had fails on day one and gets switched off.
Adopt the architecture you **do** have, then tighten it one reviewed step at a time.

## 1. Inventory

- List every production project and what it references. `architests graph` prints the graph once the
  policy can find the projects.
- Group projects into responsibilities that describe what they are for, not where they sit. A
  qualified name (`Persistence.SqlServer`) belongs to `Persistence`; only the first segment counts.
- Name the composition root with `category: "composition"` and hosts with `category: "host"`.

## 2. Write a permissive first policy

Start with the full dependency closure permitted, so the first run fails only on what you are ready
to fix.

```json
{
  "version": 3,
  "sourceRoots": ["src"],
  "responsibilities": [
    {
      "name": "Domain",
      "category": "domain",
      "purpose": "The domain model and its invariants.",
      "mayDependOn": ["Application", "Persistence", "Composition"],
      "mustNotDependOn": []
    }
  ]
}
```

Set `sourceRoots` correctly first. An empty graph satisfies every rule, which is why `graphDiscovery`
fails instead of passing silently.

## 3. Record the API before enforcing it

If the public API is already shipped, capture it before enabling `apiRules`:

```bash
architests api capture
```

Then enable the group. The first `apiSurface` run compares against the API you actually have, so it
passes, and every later change becomes a reviewed diff.

## 4. Waivers, not skips

When a rule finds a real departure you cannot fix today, record it:

```json
"waivers": [
  {
    "project": "Legacy.Reporting",
    "mapsTo": "Reporting",
    "rules": ["naming"],
    "reason": "The project name predates the catalogue and a published pipeline references it.",
    "reviewedOn": "2026-10-18"
  }
]
```

- `mapsTo` keeps every dependency rule in force; only the name is excused.
- Excuse one rule, not the whole set.
- Set `waiverPolicy.maxAgeDays` so a waiver must be renewed. An unreviewed waiver is how a policy
  quietly stops being true.

`SkipRule` removes a rule for the whole repository with no record. Treat it as a last resort, never as
the adoption strategy.

## 5. Start the dependency budget empty

`dependencyBudget` is a ratchet on the project graph. It records the references each project may
declare today, so a new dependency needs a reviewed policy change.

```json
"dependencyBudget": {
  "enabled": true,
  "failOnStale": true,
  "entries": []
}
```

The rule is enabled and checks nothing, which is exactly right: the mechanism is in place. Add one
entry at a time, listing only the references a project has today.

| Field | Default | Meaning |
| -- | -- | -- |
| `enabled` | `false` | Whether the budget is enforced. |
| `failOnStale` | `true` | Fail when an allowed reference no longer exists. |
| `entries[].project` | — | The production project the entry budgets. |
| `entries[].references` | — | The project references the budget permits. |

- A reference outside the entry is a violation.
- With `failOnStale`, an allowance the project no longer uses is also a violation. Tighten the
  budget: a stale allowance hides the next dependency.
- The rule is **not waivable**; the budget is the record that keeps growth reviewable.
- A project with no entry is not budgeted, so adoption can proceed one project at a time.

## 6. Tighten one step at a time

Each step is a small pull request that keeps the suite green:

1. **Naming.** Add aliases or waivers, then rename projects in a dedicated change.
2. **Forbidden packages.** Add one prefix to one responsibility, fix, repeat. The rule checks the
   declared references and the closure restore resolved, so a transitive or centrally injected
   package is caught too.
3. **Dependency direction.** Move one edge from `mayDependOn` to `mustNotDependOn`, or remove it from
   `mayDependOn` so silence becomes a violation.
4. **Type rules.** Enable `typeRules` for one category with `sealConcreteClasses` and generated-code
   exclusions, fix, then widen the scope.
5. **Ports.** Move an interface into `domain` or `application` only when an adapter implements it and
   its signature is technology-neutral.
6. **Reachability.** Turn on `policyIntegrity.checkReachability` once the matrix is honest; it finds
   prohibitions that a permitted path bypasses.

A rule that cannot pass even with waivers is telling you the policy is wrong, not the code. Change
the policy.

## Reading a failure during migration

- `naming` — add an alias for an established name, or a waiver with `mapsTo`.
- `dependencyMatrix` / `assemblyDependencyMatrix` — the message names the reference, the direction
  and the likely fix. Prefer moving a type over widening `mayDependOn`.
- `apiSurface` — expected after an intentional API change: run `architests api capture` and review the
  diff before committing.
- `dependencyBudget` — a new reference is over budget; a stale allowance must be removed.
- `policyIntegrity` — the policy contradicts itself; fix the policy before the code.

## In the sample

[`ArchitectureSample`](https://github.com/purview-dev/architests/tree/main/src/samples/ArchitectureSample)
demonstrates the end state: a permissive-but-honest matrix, a committed API baseline, a narrow `naming`
waiver for `Legacy.Reporting`, and a dependency budget with one entry per production project.
