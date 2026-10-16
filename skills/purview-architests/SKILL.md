---
name: purview-architests
description: Adopt or debug Purview.Architecture.Testing, the package that enforces a repository's project and type-level dependency policy as TUnit tests. Use when adding architecture tests to a repository, writing or changing .config/architecture-policy.json, enabling the assembly rule, recording a waiver, interpreting a dependency-matrix, type-level, cycle, sibling-independence, composition-boundary, forbidden-package, framework-reference or naming failure, or when the architecture tests pass but enforce nothing.
license: MIT
metadata:
  product: Purview.Architecture.Testing
  repository: purview-dev/architests
---

# Architecture tests with Purview.Architecture.Testing

The package defines the rules once so a policy is not copied into every repository's test project.

## Adoption is two things

**1. A policy file** at `.config/architecture-policy.json`:

```json
{
  "$schema": "https://raw.githubusercontent.com/purview-dev/architests/main/architecture-policy.schema.json",
  "version": 2,
  "sourceRoots": ["src"],
  "responsibilities": [
    {
      "name": "Core",
      "aliases": ["Domain"],
      "category": "domain",
      "purpose": "The domain model, its invariants and its rules.",
      "mayDependOn": [],
      "mustNotDependOn": ["Application", "Persistence"],
      "forbiddenFrameworkReferences": ["Microsoft.AspNetCore.App"]
    }
  ]
}
```

**2. One derived test class:**

```csharp
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests;
```

## `[InheritsTests]` is load-bearing

TUnit does **not** walk base types during discovery. Without the attribute the class compiles, the
run is green, and nothing is enforced — the worst failure this package can have.

TUnit's analyzer reports `TUnit0030` when it is missing. Treat it as an error, not a suggestion.

If the architecture tests "pass" but you cannot name which rule ran, check this first.

## The rules

| Rule id | Test | What it catches |
| -- | -- | -- |
| `graphDiscovery` | `Repository_GivenItsSourceRoots_YieldsProjectsToCheck` | An empty graph — which satisfies every other rule |
| `naming` | `ProductionProjects_GivenTheirNames_MapToAResponsibility` | A project name matching no responsibility or alias |
| `discouragedName` | `ProductionProjects_GivenTheirNames_AvoidCatchAllNames` | A project named after a category, so nothing can be kept out of it |
| `dependencyMatrix` | `ProductionProjects_GivenTheirReferences_RespectTheDependencyMatrix` | A declared `ProjectReference` the matrix forbids or does not permit |
| `assemblyDependencyMatrix` | `AssemblyTypes_GivenTheirReferences_RespectTheDependencyMatrix` | A **type** in one responsibility using a type in another, including a dependency that only arrives transitively or through a package |
| `siblingIndependence` | `SiblingImplementations_GivenTheirReferences_DoNotDependOnEachOther` | A qualified implementation referencing a qualified sibling |
| `compositionBoundary` | `Implementations_GivenTheirReferences_DoNotDependOnComposition` | A non-host project referencing the composition root (`category == "composition"`) |
| `forbiddenPackage` | `ProductionProjects_GivenTheirPackages_AvoidForbiddenFrameworkDependencies` | A package matching a forbidden prefix for that responsibility |
| `forbiddenFramework` | `ProductionProjects_GivenTheirFrameworkReferences_AvoidForbiddenFrameworks` | A `<FrameworkReference>` matching a forbidden prefix, e.g. `Microsoft.AspNetCore.App` in a portable layer |
| `cycle` | `ProjectGraph_GivenItsReferences_HasNoCycles` | A dependency cycle, reported readably |
| `testReference` | `ProductionProjects_GivenTheirReferences_DoNotReferenceATestProject` | Production code referencing a test project |
| `waiverIntegrity` | `PolicyWaivers_GivenTheirDefinitions_AreJustifiedAndStillApply` | A waiver that is stale, unjustified, malformed, or excuses an unknown rule id |

A separate test validates the policy against the schema first, because a policy that does not match
its schema may be silently missing a rule.

`graphDiscovery` runs before the rules it protects. If `sourceRoots` is wrong, discovery finds
nothing and every other rule passes vacuously — that is what this rule exists to stop.

## The type-level rule

A project file only shows a declared `ProjectReference`. `assemblyDependencyMatrix` reads the
compiled assemblies by **metadata only** — no assembly is loaded or executed — and checks every
external type each one references against the same `mayDependOn` / `mustNotDependOn` matrix. It
catches what a project file cannot: a type that only arrives transitively, or through a package.

```json
"assemblyRules": {
  "enabled": true,
  "assemblyRoots": [],
  "assemblyGlobs": ["*.dll"]
}
```

- **Off by default.** It needs a build to have produced the assemblies, so it is opt-in.
- An empty `assemblyRoots` scans the test output directory, which already holds every project the
  test project references, directly or transitively. Set it to repo-relative folders only if the
  assemblies are somewhere else.
- Semantics mirror `dependencyMatrix`: same-responsibility references are always allowed; a
  reference the matrix neither permits nor forbids is a violation.
- Enabled with nothing found is a **failure**, not a silent pass — the same guard `graphDiscovery`
  provides for projects.
- Qualified implementations collapse into their responsibility, so `Persistence.SqlServer` is
  `Persistence` at the type level too. Qualification is a naming choice, never a different rule.

Assemblies are attributed to projects by the SDK's generated `<ProjectName>Assembly` marker when a
project's `AssemblyName` is unrelated to its name, and by name otherwise.

## Classifying test and production projects

The package decides which projects the rules govern from the project graph:

- A project is a **test project** when its name ends with `Tests`, **or** its path contains a
  `tests` directory. Test projects are excluded from every rule — a test references everything by
  design, so applying the matrix to it would make the rule set unusable.
- Everything else is a **production project**, and every rule applies to it.

## Prefer a waiver to a skip

```csharp
protected override bool SkipRule(string rule) => rule == ArchitectureRules.DiscouragedName;
```

`SkipRule` removes a rule for the whole repository, with no record. A waiver is better: it is per
project, carries a reason and a review date, keeps every other rule in force for that project, and
is itself checked by `waiverIntegrity`.

```json
"waivers": [
  {
    "project": "Legacy.Utilities",
    "mapsTo": "Shared",
    "rules": ["naming"],
    "reason": "Predates the catalogue; renaming breaks a published package id.",
    "reviewedOn": "2026-01-14"
  }
]
```

`mapsTo` is the useful part: it maps an established project onto the responsibility it actually has,
so the dependency rules still apply in full and only the *name* is excused.

Any rule id may be waived, including `siblingIndependence` and `assemblyDependencyMatrix`. An
unknown id is a `waiverIntegrity` failure, so a typo cannot silently excuse nothing.

## Adding your own rule

A repository can add a bespoke check without a second architecture framework. Implement
`IArchitectureRule` and return the same `ArchitectureViolation` type as the built-in rules:

```csharp
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests
{
    protected override IReadOnlyList<IArchitectureRule> AdditionalRules => [new NoConsoleLoggingRule()];

    sealed class NoConsoleLoggingRule : IArchitectureRule
    {
        public string Rule => "noConsoleLogging";

        public IReadOnlyList<ArchitectureViolation> Evaluate(ArchitectureContext context) =>
            // context.Policy, context.Projects and context.Assemblies
            [];
    }
}
```

Each additional rule becomes its own `[Test]` (`AdditionalRule_GivenTheRepository_RespectsThePolicy`),
so a failure names the rule. It participates in the same failure reporting, `SkipRule` (by its
`Rule` id) and waiver model — a waiver whose `rules` names it is honoured automatically.

The `purview-architests-extending` skill covers the full extension point and the package's internals.

## Reading a failure

Each rule is its own test, so a failure names the rule rather than collapsing the set into one
assertion. The `.Because(...)` text lists every violation the rule found.

- `naming` — a project name is `<Responsibility>[.<Implementation>]`, and only the **first segment**
  is classified. `Persistence.SqlServer` and `Persistence` are both Persistence. Add an alias, or a
  waiver with `mapsTo`.
- `dependencyMatrix` / `assemblyDependencyMatrix` — fix the reference, or move the type. Widening
  `mayDependOn` is the last resort, and it is a policy change that needs review.
- `forbiddenFramework` — a `<FrameworkReference>` on a project that should not have it. Move it to
  the responsibility that owns that technology.
- `graphDiscovery` — almost always a wrong `sourceRoots`. An empty array scans the repository root,
  which suits a flat layout.
- `assemblyDependencyMatrix` reporting "no compiled assemblies" — the projects were not built, or
  `assemblyRoots` points at the wrong folder.

## The three layers

Useful when extending the package rather than consuming it:

- `ArchitecturePolicy` / `ProjectGraph` / `AssemblyGraph` — read the policy, the `.csproj` graph and
  the compiled assembly metadata. No MSBuild and no test-framework types.
- `ArchitecturePolicyEvaluator` — the rules, returning typed `ArchitectureViolation` lists. It takes
  an **in-memory graph**, so a rule is tested as a three-project fixture rather than a repository on
  disk.
- `PurviewArchitectureTests` — the TUnit wrapper, one `[Test]` per rule.

Project files are read because a declared `ProjectReference` is what a reviewer adds and what the
policy governs; compiled assemblies are read separately because they catch what the project file
does not show.

## Overrides

```csharp
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests
{
    protected override string? RepositoryRoot => "/path/to/repo";
}
```

`RepositoryRoot` defaults to probing upward from the test binary for the policy file. The search
**stops at the repository boundary** (`.git`) rather than continuing into parent folders — walking
past it would happily find a sibling checkout's policy and validate this repository against someone
else's rules.

## Policy schema version

The current policy document version is **2**. Version 2 removed the `rules` object
(`noCycles`, `noProductionToTestReferences`, `implementationsMustNotReferenceComposition`,
`siblingImplementationsIndependent`), whose flags were deserialised but never consulted. Use
`SkipRule` or a waiver to opt out of a rule. A version-1 policy that set `rules` must delete the
block; a version-1 policy that did not still validates.
