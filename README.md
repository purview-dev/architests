# Purview.Architecture.Testing

[![NuGet](https://img.shields.io/nuget/v/Purview.Architecture.Testing.svg)](https://www.nuget.org/packages/Purview.Architecture.Testing)
[![Release](https://github.com/purview-dev/architests/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/architests/actions/workflows/release.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.md)

Enforce a repository's architecture policy as TUnit tests, so the rules are defined once rather than
copied into every repository's test project.

- **Project-level dependency direction** — a declared `ProjectReference` the matrix forbids, or
  does not permit.
- **Type-level dependency direction** — a type in one responsibility using a type in another,
  including a dependency that only arrives transitively or through a package. Read from the compiled
  assemblies by metadata only: nothing is loaded or executed.
- **Composition boundary** — only the composition root's category is exempt from being referenced
  back to.
- **Sibling independence** — qualified implementations of a responsibility must not reference one
  another.
- **Forbidden packages and framework references** — e.g. `Microsoft.AspNetCore.App` in a portable
  layer.
- **Cycles, naming and test references** — the rest of the standard set, reported readably.
- **Waivers** — reviewed, per-project departures that keep every other rule in force and are
  themselves checked.
- **An extension point** — add a bespoke rule to the same reporting, `SkipRule` and waiver model,
  without a second architecture framework.

Adoption is two things: a policy file and one derived class.

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

```csharp
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests;
```

> **`[InheritsTests]` is required.** TUnit does not walk base types during discovery, so without it
> the class compiles, the run is green and nothing is enforced. TUnit's analyzer reports `TUnit0030`
> — treat it as an error.

## Install

```text
dotnet add package Purview.Architecture.Testing
```

## Documentation

- [Getting started](docs/Getting-Started.md) — adopt the package in a repository, step by step.
- [Policy reference](docs/Policy-Reference.md) — every field, every rule id, every default.
- [Type-level rules](docs/Type-Level-Rules.md) — how the assembly check works and how an assembly
  is attributed to a project.
- [Extending](docs/Extending.md) — add your own rule to the standard set.
- [The policy schema](architecture-policy.schema.json) — the published JSON Schema.

## Sample

[`src/samples/ArchitectureSample`](src/samples/ArchitectureSample/README.md) is a small, fully
working repository that adopts the package: four projects, a policy, and a test project whose whole
suite is one derived class.

```bash
dotnet test src/samples/ArchitectureSample/tests/Sample.ArchitectureTests/Sample.ArchitectureTests.csproj
```

## Repository

- `src/src/ArchitectureTesting` — the package: policy reader, project and assembly graph, rule
  evaluator, and the TUnit base class.
- `src/tests/ArchitectureTesting.UnitTests` — rule tests against in-memory and fixture graphs.
- `src/tests/Repository.UnitTests` — the skill self-tests and the package applied to this repository.
- `src/samples/ArchitectureSample` — the adoption sample.
- `architecture-policy.schema.json` — the published policy schema, embedded into the package.
- `skills/` — the agent skills this repository publishes.

## Agent skills

```bash
npx skills add purview-dev/architests
```

## Build

```text
dotnet tool restore
just restore
just build
just test
just lint-check
just pack
just pipeline-pr
```

Part of [Purview.BuildSdk](https://github.com/purview-dev/build-sdk) · [purview.dev](https://purview.dev/projects/architests/)
