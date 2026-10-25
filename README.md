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
- **Type naming and shape** — interface, attribute and exception conventions; sealed classes,
  read-only collections, root namespaces and attributes, read from metadata.
- **Cycles, naming and test references** — the rest of the standard set, reported readably.
- **Waivers** — reviewed, per-project departures that keep every other rule in force and are
  themselves checked.
- **An extension point** — add a bespoke rule to the same reporting, `SkipRule` and waiver model,
  without a second architecture framework.

Adoption is two things: a policy file and one derived class.

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

The same policy can be checked outside a test run:

```bash
dotnet tool install --global Purview.Architecture.Testing.Cli
architests check                  # every rule, as the test suite would
architests check --format sarif   # for GitHub code scanning
architests validate
architests waivers list --stale
```

Exit codes are part of the contract: `0` clean, `1` findings, `2` a malformed command line.

## Rules

| Rule id | Catches |
| -- | -- |
| `policyIntegrity` | A policy that names an unknown responsibility, permits and forbids the same dependency, or declares a duplicate name or alias |
| `graphDiscovery` | No production projects discovered, so every other rule would pass vacuously |
| `naming` | A project name that maps to no responsibility or alias |
| `discouragedName` | A project named after a category rather than a responsibility |
| `dependencyMatrix` | A declared `ProjectReference` the matrix forbids or does not permit |
| `assemblyDependencyMatrix` | A type-level reference the matrix forbids or does not permit, including one that arrives transitively |
| `typeNaming` | A public interface without `I`, an attribute or exception without its suffix, or a discouraged suffix such as `Manager` |
| `typeShape` | An unsealed public class, a public field, a mutable collection return, or a utility class that is not static |
| `typeNamespace` | A public type declared outside its project's root namespace |
| `attributePolicy` | A missing or forbidden attribute, `[Obsolete]` without a message, or `InternalsVisibleTo` naming production code |
| `apiSurface` | The public API differs from the committed baseline |
| `apiDocumentation` | A public type or member has no XML documentation |
| `portImplementation` | A port in an inner layer has no implementation |
| `portPurity` | A port exposes an infrastructure technology |
| `dependencyBudget` | A project references beyond its budget, or the budget still allows a reference that has gone |
| `siblingIndependence` | A qualified implementation referencing a qualified sibling |
| `compositionBoundary` | A non-composition, non-host project referencing the composition root |
| `forbiddenPackage` | A package the responsibility owns matching a forbidden prefix |
| `forbiddenFramework` | A framework reference matching a forbidden prefix for the responsibility |
| `cycle` | A cycle in the project graph |
| `testReference` | A production project referencing a test project |
| `waiverIntegrity` | A stale, unjustified or malformed waiver, or one excusing an unknown or unwaivable rule |

## Documentation

- [Getting started](docs/Getting-Started.md) — adopt the package in a repository, step by step.
- [Tutorial](docs/Tutorial.md) — grow one application and its policy from a two-project matrix to type, API, port and budget rules.
- [Policy reference](docs/Policy-Reference.md) — every field, every rule id, every default.
- [Type-level rules](docs/Type-Level-Rules.md) — how the assembly check works and how an assembly
  is attributed to a project.
- [API contracts](docs/Contracts.md) — the committed baseline, XML documentation and the port rules.
- [Migration and budget](docs/Migration.md) — adopt incrementally and ratchet the project graph.
- [Extending](docs/Extending.md) — add your own rule to the standard set.
- [Command line](docs/Cli.md) — validate, capture the API, review waivers and render the graph.
- [The policy schema](architecture-policy.schema.json) — the published JSON Schema.

## Contributing and security

- [Contributing](CONTRIBUTING.md) — how to get set up, and what a pull request needs.
- [Security policy](SECURITY.md) — how to report a vulnerability privately, and the design
  commitments the package maintains (metadata-only assembly reading, no network, no process
  launch, no regex).
- [Changelog](CHANGELOG.md) — what changed, and what it means for a consumer.
- [Code of conduct](CODE_OF_CONDUCT.md)

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
- `src/src/Architecture.CLI` — the `architests` dotnet tool.
- `src/tests/ArchitectureTesting.UnitTests` — rule tests against in-memory and fixture graphs.
- `src/tests/ArchitectureTesting.AcceptanceTests` — BDTest Given/When/Then scenarios, pairing a
  happy and an unhappy path for each rule they cover.
- `src/tests/ArchitectureTesting.CLI.UnitTests` — the tool's parser and commands.
- `src/tests/Repository.UnitTests` — the skill self-tests, the rule/documentation drift test, and the
  package applied to this repository.
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
