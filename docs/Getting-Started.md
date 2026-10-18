# Getting started

## 1. Add the package

```text
dotnet add package Purview.Architecture.Testing
```

Add it to a test project that already references the projects you want to govern, or create a
dedicated `Architecture.UnitTests` project. The test project must reference the production projects,
directly or transitively, because the type-level rule reads their compiled assemblies from the test
output.

## 2. Write the policy

Create `.config/architecture-policy.json` at the repository root. The smallest useful policy names
each responsibility and what it may depend on:

```json
{
  "$schema": "https://raw.githubusercontent.com/purview-dev/architests/main/architecture-policy.schema.json",
  "version": 3,
  "sourceRoots": ["src"],
  "responsibilities": [
    {
      "name": "Domain",
      "category": "domain",
      "purpose": "The domain model, its invariants and its rules.",
      "mayDependOn": [],
      "mustNotDependOn": ["Application", "Persistence", "Composition"]
    },
    {
      "name": "Application",
      "category": "application",
      "purpose": "Coordinates the use cases and workflows.",
      "mayDependOn": ["Domain"],
      "mustNotDependOn": ["Persistence", "Composition"]
    },
    {
      "name": "Persistence",
      "category": "infrastructure",
      "purpose": "Durable storage and persistence-specific configuration.",
      "mayDependOn": ["Domain", "Application"],
      "mustNotDependOn": ["Composition"]
    },
    {
      "name": "Composition",
      "category": "composition",
      "purpose": "The composition root that selects and wires the implementations.",
      "mayDependOn": ["Domain", "Application", "Persistence"],
      "mustNotDependOn": []
    }
  ]
}
```

A project name is `<Responsibility>[.<Implementation>]`, and only the **first segment** is
classified: `Persistence.SqlServer` and `Persistence` are both `Persistence`. Qualification is a
naming choice, never a different set of rules.

`sourceRoots` defaults to `["src"]`. Getting it wrong finds no projects, which the `graphDiscovery`
rule reports rather than letting every other rule pass vacuously.

## 3. Add the test class

```csharp
using Purview.Architecture.Testing;

[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests;
```

That is the whole suite. Each rule is a separate `[Test]`, so a failure names the rule rather than
collapsing the set into one assertion.

> **`[InheritsTests]` is required.** TUnit does not walk base types during discovery, so without the
> attribute the class compiles, the run is green, and nothing is enforced — the worst failure this
> package can have. TUnit's analyzer reports `TUnit0030`; treat it as an error.

## 4. Run it

```bash
dotnet test
```

If `graphDiscovery` fails, `sourceRoots` is wrong. If `policyIntegrity` fails, the policy itself
contradicts: an unknown name, a dependency both permitted and forbidden, or a duplicate name or
alias. If `naming` fails, a project name does not map to a responsibility — add an alias or a waiver.
If `dependencyMatrix` or `assemblyDependencyMatrix` fails, the message names the offending reference,
the direction the dependency should run, and the likely fix.

The same policy can be checked without a test run, which is useful in a pre-commit hook or a CI step
that does not build the solution:

```bash
dotnet tool install --global Purview.Architecture.Testing.Cli
architests validate
```

## 5. Turn on the type-level rule

The project-level matrix only sees declared `ProjectReference`s. To also catch a type that arrives
transitively or through a package, enable the assembly check:

```json
"assemblyRules": {
  "enabled": true
}
```

An empty `assemblyRoots` scans the test output directory, which already holds every project the test
project references. See [Type-level rules](Type-Level-Rules.md) for how an assembly is attributed to
a project.

## 6. Turn on the type rules (optional)

The same compiled assemblies reveal the public API a project declares. To police it:

```json
"typeRules": {
  "enabled": true,
  "appliesTo": { "categories": ["domain", "application", "shared"] }
}
```

This enables `typeNaming`, `typeShape`, `typeNamespace` and `attributePolicy`. They check only public
types, skip generated code, and produce no test case when the group is off. See the
[policy reference](Policy-Reference.md#type-rules) for every setting.

## 7. Commit the API baseline (optional)

To make a change to the public API a reviewed event, commit a baseline:

```json
"apiRules": {
  "enabled": true
}
```

Then write it with the tool and commit the result:

```bash
dotnet tool install --global Purview.Architecture.Testing.Cli
architests api capture
architests api check
```

`apiSurface` compares every build with `.config/api-surface.json`; `apiDocumentation` requires a
`<summary>` on every public type. Enable `portRules` as well to require an implementation for each
interface in the `domain` and `application` categories and to reject infrastructure types in their
signatures. See the [policy reference](Policy-Reference.md#api-rules).

Adopting in a repository that already has tangled dependencies? Start with
[Migration and the dependency budget](Migration.md): describe today's graph, then tighten one rule at
a time. The contract rules are covered in [API contracts](Contracts.md).

## Overrides

```csharp
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests
{
    protected override string? RepositoryRoot => "/path/to/repo";
}
```

`RepositoryRoot` defaults to probing upward from the test binary for the nearest policy file. The
search **stops at the repository boundary** (`.git`) rather than continuing into parent folders —
walking past it would happily find a sibling checkout's policy and validate this repository against
someone else's rules.

`SkipRule` opts a whole repository out of a rule. Prefer a [waiver](Policy-Reference.md#waivers): it
is per project, carries a reason and a review date, and is itself checked.
