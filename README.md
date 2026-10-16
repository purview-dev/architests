# Purview.Architecture.Testing

[![NuGet](https://img.shields.io/nuget/v/Purview.Architecture.Testing.svg)](https://www.nuget.org/packages/Purview.Architecture.Testing)
[![Release](https://github.com/purview-dev/architests/actions/workflows/release.yml/badge.svg)](https://github.com/purview-dev/architests/actions/workflows/release.yml)

Enforces a repository's project and assembly dependency policy as TUnit tests, so the rules are
defined once rather than copied into every repository's test project.

Add a policy at `.config/architecture-policy.json`, add one derived test class, and the rules run:
dependency direction at both project and type level, sibling independence, composition boundaries,
forbidden packages and framework references, cycles, naming and test-reference checks.

```csharp
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests;
```

## Install

```text
dotnet add package Purview.Architecture.Testing
```

See the [package README](src/src/ArchitectureTesting/README.md) for the full policy reference and
[`architecture-policy.schema.json`](architecture-policy.schema.json) for the schema.

## Repository

- `src/src/ArchitectureTesting` — the package: policy reader, project and assembly graph, rule
  evaluator, and the TUnit base class.
- `src/tests/ArchitectureTesting.UnitTests` — rule tests against in-memory and fixture graphs.
- `src/tests/Repository.UnitTests` — the skill self-tests and the package applied to this repository.
- `architecture-policy.schema.json` — the published policy schema, embedded into the package.
- `skills/purview-architests` — the agent skill this repository publishes.

## Agent skills

```bash
npx skills add purview-dev/architests
```

## Build

```text
dotnet tool restore
just build
just test
just pack
```
