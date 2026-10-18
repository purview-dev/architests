# Purview.Architecture.Testing

Enforces a repository's project and assembly dependency policy as TUnit tests, so the rules are
defined once rather than copied into every repository's test project.

## Install

```bash
dotnet add package Purview.Architecture.Testing
```

## Adopt

Two things. A policy at `.config/architecture-policy.json`:

```json
{
  "$schema": "https://raw.githubusercontent.com/purview-dev/architests/main/architecture-policy.schema.json",
  "version": 3,
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

And one derived test class:

```csharp
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests;
```

> **`[InheritsTests]` is required.** TUnit does not walk base types during discovery, so without it
> the class compiles, the run is green and nothing is enforced. TUnit's analyzer reports `TUnit0030`
> — treat it as an error.

## The rules

| Rule | Catches |
| -- | -- |
| `policyIntegrity` | A policy that names an unknown responsibility, permits and forbids the same dependency, or declares a duplicate name or alias. Not waivable. |
| `graphDiscovery` | An empty graph, which satisfies every other rule |
| `naming` | A project name matching no responsibility or alias |
| `discouragedName` | A project named after a category, so nothing can be kept out of it |
| `dependencyMatrix` | A declared `ProjectReference` the matrix forbids or does not permit |
| `assemblyDependencyMatrix` | A **type** in one responsibility using a type in another, including a dependency that arrives transitively or through a package |
| `typeNaming` | A public interface without `I`, an attribute or exception without its suffix, or a discouraged suffix |
| `typeShape` | An unsealed public class, a public field, a mutable collection return, or a utility class that is not static |
| `typeNamespace` | A public type declared outside its project's root namespace |
| `attributePolicy` | A missing or forbidden attribute, `[Obsolete]` without a message, or `InternalsVisibleTo` naming production code |
| `apiSurface` | The public API differs from the committed baseline |
| `apiDocumentation` | A public type or member has no XML documentation |
| `portImplementation` | A port in an inner layer has no implementation |
| `portPurity` | A port exposes an infrastructure technology |
| `dependencyBudget` | A project references beyond its budget, or the budget still allows a reference that has gone |
| `siblingIndependence` | A qualified implementation referencing a qualified sibling |
| `compositionBoundary` | A non-host project referencing the composition root (`category == "composition"`) |
| `forbiddenPackage` | A package matching a forbidden prefix for that responsibility |
| `forbiddenFramework` | A `<FrameworkReference>` matching a forbidden prefix, e.g. `Microsoft.AspNetCore.App` in a portable layer |
| `cycle` | A dependency cycle, reported readably |
| `testReference` | Production code referencing a test project |
| `waiverIntegrity` | A waiver that is stale, unjustified, malformed, or excuses an unknown rule |

The policy is validated against its schema first, because a policy that does not match its schema
may be silently missing a rule. `policyIntegrity` then checks what a schema cannot express: unknown
responsibility names, a dependency that is both permitted and forbidden, duplicate names or aliases,
and — with `checkReachability` — a prohibition the permitted dependencies can bypass.

```json
"policyIntegrity": {
  "checkReachability": true
}
```

## Waiver expiry

`reviewedOn` is a note until the policy gives it a maximum age:

```json
"waiverPolicy": {
  "maxAgeDays": 180
}
```

A waiver reviewed longer ago than that is a `waiverIntegrity` failure, and
`architests waivers list --stale` lists the ones that are due. Expiry is off by default, so an
existing policy keeps passing until it adopts a maximum.

## The type-level rule

A project file only shows a declared `ProjectReference`. `assemblyDependencyMatrix` reads the
compiled assemblies by metadata only — no assembly is loaded or executed — and checks every external
type each one references against the same `mayDependOn` / `mustNotDependOn` matrix, so a dependency
that only arrives transitively or through a package is caught.

```json
"assemblyRules": {
  "enabled": true,
  "assemblyRoots": [],
  "assemblyGlobs": ["*.dll"]
}
```

- **Off by default**, because it needs a build to have produced the assemblies.
- An empty `assemblyRoots` scans the test output directory, which already holds every project the
  test project references, directly or transitively.
- Enabled with nothing found is a failure, not a silent pass.
- Semantics mirror `dependencyMatrix`: same-responsibility references are always allowed, and a
  reference the matrix neither permits nor forbids is a violation. Qualified implementations
  collapse into their responsibility at the type level too.

## Type rules

The same reader exposes each assembly's declarations, so the public surface can be policed too:

```json
"typeRules": {
  "enabled": true,
  "appliesTo": { "categories": ["domain", "application", "shared"] },
  "excludeGeneratedCode": true
}
```

| Rule | Catches |
| -- | -- |
| `typeNaming` | An interface without `I`, an attribute or exception without its suffix, a discouraged suffix |
| `typeShape` | An unsealed public class, a public instance field, a mutable collection return, a non-static utility class |
| `typeNamespace` | A public type outside the project's `RootNamespace` |
| `attributePolicy` | A missing or forbidden attribute, `[Obsolete]` without a message, `InternalsVisibleTo` naming production code |

It is **off by default** because it needs a build. Only public types are checked; generated code is
skipped, and `appliesTo.categories` narrows the responsibility scope. Each enabled rule is its own
test, and a disabled group produces no test rather than one that checks nothing.

## API and port rules

`apiRules` commits the public API and checks it has not drifted:

```json
"apiRules": {
  "enabled": true
}
```

`apiSurface` compares the build with `.config/api-surface.json`; `apiDocumentation` requires an XML
documentation entry for every public type. Write the baseline with the tool, never by hand:

```bash
dotnet tool install --global Purview.Architecture.Testing.Cli
architests api capture
architests api check
```

`portRules` treats interfaces in the `domain` and `application` categories as ports:
`portImplementation` requires each one to have a public implementation, and `portPurity` rejects a
signature that names an infrastructure technology such as `Microsoft.EntityFrameworkCore`.

Both groups are off by default and need a build. See the
[policy reference](https://github.com/purview-dev/architests/blob/main/docs/Policy-Reference.md#api-rules).

## Test and production classification

- A project is a **test project** when its name ends with `Tests`, **or** its path contains a
  `tests` directory. Test projects are excluded from every rule.
- Everything else is a **production project**, and every rule applies to it.

## Waivers over skips

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

`SkipRule` removes a rule for the whole repository with no record. A waiver is per project, carries
a reason and a review date, keeps every other rule in force, and is itself checked. `mapsTo` maps an
established project onto the responsibility it actually has, so only the *name* is excused.

Any rule id may be waived. An unknown id is a `waiverIntegrity` failure, so a typo cannot silently
excuse nothing.

## Adding your own rule

Implement `IArchitectureRule` and return the same `ArchitectureViolation` type as the built-in
rules, then list it in `AdditionalRules`:

```csharp
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests
{
    protected override IReadOnlyList<IArchitectureRule> AdditionalRules => [new NoConsoleLoggingRule()];
}
```

Each additional rule becomes its own `[Test]` and participates in the same failure reporting,
`SkipRule` and waiver model.

## Sample and documentation

- A fully working sample repository:
  [`src/samples/ArchitectureSample`](https://github.com/purview-dev/architests/tree/main/src/samples/ArchitectureSample).
- Guides: [getting started](https://github.com/purview-dev/architests/blob/main/docs/Getting-Started.md),
  [policy reference](https://github.com/purview-dev/architests/blob/main/docs/Policy-Reference.md),
  [type-level rules](https://github.com/purview-dev/architests/blob/main/docs/Type-Level-Rules.md),
  [extending](https://github.com/purview-dev/architests/blob/main/docs/Extending.md).

## Design

Three layers, deliberately separated: `ArchitecturePolicy`/`ProjectGraph`/`AssemblyGraph` read the
policy, the `.csproj` graph and compiled assembly metadata with no MSBuild or test-framework types;
`ArchitecturePolicyEvaluator` holds the rules and takes an in-memory graph, so a rule is testable as
a three-project fixture; `PurviewArchitectureTests` is the TUnit wrapper with one `[Test]` per rule.

Project files are read because a declared `ProjectReference` is what a reviewer adds and what the
policy governs; compiled assemblies are read separately because they catch what the project file
does not show.

## Policy schema version

The current document version is **3**. Version 3 adds `typeRules`, `policyIntegrity` and
`waiverPolicy`, all optional, so a version-2 policy remains valid. Version 2 removed the `rules` object, whose flags were
deserialised but never consulted; use `SkipRule` or a waiver to opt out of a rule. A version-1
policy that set `rules` must delete the block; one that did not still validates.

## Command line

The package ships a companion tool, so a policy can be checked without a test run:

```bash
dotnet tool install --global Purview.Architecture.Testing.Cli
architests validate
architests waivers list --stale
```

---

Part of [Purview.BuildSdk](https://github.com/purview-dev/build-sdk) · [purview.dev](https://purview.dev/projects/architests/)
