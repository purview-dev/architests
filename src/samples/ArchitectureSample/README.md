# Architecture sample

A small, fully working repository that adopts `Purview.Architecture.Testing`. It is a repository
inside the repository: it has its own `.config/architecture-policy.json`, its own source layout and
its own test project, so it demonstrates exactly what a consumer does.

```
ArchitectureSample/
├── .config/architecture-policy.json     the policy — the single definition of the rules
├── src/
│   ├── Domain/                          the domain model, depends on nothing
│   ├── Application/                     use cases, depends on Domain
│   ├── Persistence/                     storage, depends on Domain
│   └── Composition/                     the composition root, depends on all three
└── tests/Sample.ArchitectureTests/      one derived class; the whole suite
```

The entire suite is one line:

```csharp
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests;
```

`[InheritsTests]` is required: TUnit does not walk base types during discovery, so without it the
class compiles, the run is green, and nothing is enforced.

## What the policy enforces here

| Rule | What it checks in this sample |
| -- | -- |
| `dependencyMatrix` | `Domain` depends on nothing; `Application` and `Persistence` may depend on `Domain`; `Composition` may depend on all three |
| `assemblyDependencyMatrix` | the same direction at the **type** level, read from the compiled assemblies |
| `compositionBoundary` | only `Composition` (category `composition`) may be referenced by no one — nothing points back at it |
| `forbiddenFramework` | `Domain` and `Application` must not reference `Microsoft.AspNetCore.App` |
| `cycle`, `naming`, `discouragedName`, `testReference`, `waiverIntegrity` | the rest of the standard set |

## Run it

```bash
dotnet test src/samples/ArchitectureSample/tests/Sample.ArchitectureTests/Sample.ArchitectureTests.csproj
```

To see a rule fire, add a `ProjectReference` from `Domain` to `Persistence` in
`src/Domain/Domain.csproj` and run it again: `dependencyMatrix` fails and the message names the
direction and the fix. Remove it to go green.
