# Agent Instructions

## Purpose and authority

This repository contains the `Purview.Architecture.Testing` package: a policy reader, a project and
assembly graph, a rule evaluator, and a TUnit base class that a consuming repository adopts with one
derived test class. The repository also applies that package to itself.

- This file is the repository-wide source of truth for AI agents. More-specific `AGENTS.md` files, if
  added later, take precedence for their subtrees.
- Follow explicit user instructions first, then the nearest applicable repository instructions, then
  established code patterns.
- Operate only in this repository unless the user explicitly expands the scope.
- Never read, copy, log, or commit secrets from excluded files, environment variables, user profiles,
  local configuration, test output, or provider credentials.

## Source of truth and layout

| Path | Purpose |
| --- | --- |
| `src/ArchitectureTesting.slnx` | Canonical solution for restore, build, test, and pack |
| `src/src/ArchitectureTesting` | The package: `ArchitecturePolicy`, `ProjectGraph`, `AssemblyGraph`, `ArchitecturePolicyEvaluator`, `PurviewArchitectureTests` |
| `src/tests/ArchitectureTesting.UnitTests` | Rule tests against in-memory and fixture graphs, and the reader tests |
| `src/tests/Repository.UnitTests` | Repository-level assertions: the skill self-tests and the package applied to this repository |
| `architecture-policy.schema.json` | The published policy schema, embedded into the package as a resource |
| `.config/architecture-policy.json` | This repository's own policy, exercised by the dogfood test |
| `skills/purview-architests` | The agent skill this repository publishes |
| `Directory.Packages.props` | Centrally managed NuGet versions |
| `src/Directory.Build.props` / `src/Directory.Build.targets` | Solution-wide SDK, package, analyzer, and build behavior |
| `global.json` | Required .NET SDK and Microsoft.Testing.Platform selection |
| `package.json` | Authoritative repository/package version |
| `Justfile` | Supported local workflow commands |

## Standard workflow

1. Read this file, inspect the working tree, and locate the implementation, tests, documentation, and
   existing patterns relevant to the task.
2. Confirm behavior from code and tests rather than relying on memory or documentation alone.
3. Make the smallest coherent change. Preserve public behavior unless the task explicitly changes it.
4. Update tests for fixes and behavior changes. Update documentation when public behavior changes.
5. Run the narrowest meaningful validation first, then broader validation in proportion to risk.
6. Review the diff for unrelated edits, generated noise, compatibility risks, and missing docs or tests.

## The three-layer design (non-negotiable)

Keep the layers separated, because the separation is what makes the rules testable:

- `ArchitecturePolicy` / `ProjectGraph` / `AssemblyGraph` — read the policy, the `.csproj` graph and
  compiled assembly metadata. **No MSBuild and no test-framework types.**
- `ArchitecturePolicyEvaluator` — the rules, returning typed `ArchitectureViolation` lists. It takes
  an **in-memory graph**, so every rule is testable as a small fixture rather than a repository on
  disk.
- `PurviewArchitectureTests` — the TUnit wrapper, one `[Test]` per rule.

Every new rule must be evaluable from an in-memory fixture and asserted by a small
`ArchitectureTesting.UnitTests` test, exactly as the existing rules are.

## Reading assemblies

- Read compiled assemblies by **metadata only** (`System.Reflection.Metadata`). Never `Assembly.Load`,
  `Assembly.LoadFrom`, or any API that executes code or needs a probing path.
- The `TypeReference` table is the dependency set: it captures base types, interfaces, generic
  arguments, signatures, attributes and method-body uses, including transitive dependencies.
- Attribute an assembly to a project by the SDK's generated `<ProjectName>Assembly` marker when the
  assembly name is unrelated to the project, and by name otherwise. An assembly that maps to no
  project is a package and is ignored.

## The schema is a single file

`architecture-policy.schema.json` at the repository root is both the published schema and the
resource embedded into the package. Never keep a second copy; edit the one file. A policy is
validated against it before any rule runs, so a field the reader understands but the schema rejects
is a bug in the schema.

Bump the policy document `version` when a change would break a reader that understood the previous
shape, and say so in `README.md` and the skill.

## Testing conventions

- This repository uses TUnit on Microsoft.Testing.Platform, selected in `global.json`. Do not add
  xUnit, NUnit, MSTest, or FluentAssertions patterns unless explicitly requested.
- TUnit test methods use `[Test]`, and assertion calls are awaited, for example
  `await Assert.That(actual).IsEqualTo(expected);`.
- Use `--treenode-filter` for test filtering, not `dotnet test --filter`.
- Follow the existing AAA + `Subject_Scenario_Expectation` naming.
- **All tests must pass identically on Windows, Linux, and macOS.** Never hardcode platform-specific
  paths; build paths with `Path.Combine` and friends, and keep fixtures under the temp directory.

## Build, format, test, and pack commands

Prefer the `Justfile` recipes or their equivalent commands:

```text
dotnet tool restore
just restore
just build
just test
just lint-check
just pack
just pipeline-pr
```

Local CI-equivalent validation:

```text
dotnet restore src/ArchitectureTesting.slnx
dotnet build src/ArchitectureTesting.slnx --configuration Release
dotnet test --solution src/ArchitectureTesting.slnx --configuration Release -- --treenode-filter "/*/*/*/*/"
dotnet csharpier check .
```

Use `dotnet csharpier check .` for validation and `dotnet csharpier format .` to fix formatting.

## Versioning and releases

- `package.json` is the authoritative release/package version. Do not manually diverge project versions.
- Release is automatic on push to `main`: the `Release` workflow runs the shared `Purview.Build`
  pipeline with `Release:Mode=NuGet`.
- Never create release tags or publish packages manually unless the user explicitly requests a
  documented recovery procedure.

## Agent skills

Authored skills live in the repository-root `skills/` folder. Nothing packs them: `Purview.BuildSdk`
delivers skills through the skills CLI, which clones the *git* repository and only discovers a root
`skills/` folder.

`.agents/skills/` is the CLI's install location for skills from **other** repositories, and is
gitignored. This repository's own skills are never installed into it.

Install this repository's skill with:

```bash
npx skills add purview-dev/architests
```

The single authored skill is `skills/purview-architests/`, which covers adopting, debugging and
extending `Purview.Architecture.Testing`. `src/tests/Repository.UnitTests/SkillTests.cs` validates it
on every test run through `Purview.Skills.Testing`'s rule set. The `[InheritsTests]` attribute on
that class is load-bearing — TUnit does not walk base types during discovery, so without it the run
is green and nothing is checked.

## Completion checklist

Before handing work back:

- Confirm the requested behavior and scope are satisfied.
- Confirm only intended files changed.
- Review public API, schema, and package-content implications.
- Add or update focused tests for code changes.
- Update `README.md`, the package README, the skill and the schema when public behavior changes.
- Run appropriate build, test, formatting, and pack checks in proportion to risk.
- State exactly what validation ran. If a check was skipped or blocked, give the concrete reason and
  remaining risk.
