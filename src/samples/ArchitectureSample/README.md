# Architecture sample

A small, fully working repository that adopts `Purview.Architecture.Testing`. It is a repository
inside the repository: its own `.config/architecture-policy.json`, its own layout and its own test
project, so it demonstrates exactly what a consumer does — and it exercises every rule group the
package ships.

```text
ArchitectureSample/
├── .config/
│   ├── architecture-policy.json          the policy — the single definition of the rules
│   └── api-surface.json                  the committed public API baseline
├── src/
│   ├── Domain/                           the model and IOrderRepository, the port
│   ├── Application/                      PlaceOrderHandler, depends on the port only
│   ├── Persistence.InMemory/             one adapter for the port
│   ├── Persistence.Json/                 a sibling adapter for the same port
│   ├── Composition/                      selects an adapter and wires the handler
│   └── Legacy.Reporting/                 a project named before the catalogue; waived by name
└── tests/Sample.ArchitectureTests/       one derived class; the whole suite
```

The entire suite is one line:

```csharp
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests;
```

`[InheritsTests]` is required: TUnit does not walk base types during discovery, so without it the
class compiles, the run is green, and nothing is enforced.

## What the policy demonstrates

| Group | Rules | What this sample shows |
| -- | -- | -- |
| Dependency direction | `dependencyMatrix`, `assemblyDependencyMatrix`, `siblingIndependence` | Domain depends on nothing; Application depends on Domain's port; the two Persistence adapters depend only on Domain and not on each other; Composition may depend on all of them |
| Boundaries | `compositionBoundary`, `testReference` | nothing references Composition; production code never references the test project |
| Packages and frameworks | `forbiddenPackage`, `forbiddenFramework` | Domain and Application forbid `Microsoft.EntityFrameworkCore` and `Microsoft.AspNetCore.App`; package checks read restore output and blame only the packages a project owns, not those inherited through a permitted project reference |
| Policy integrity | `policyIntegrity` (with `checkReachability`), `graphDiscovery`, `naming`, `discouragedName`, `cycle`, `waiverIntegrity` | the matrix is internally consistent and no permitted path bypasses a prohibition |
| Waivers | `naming` via `Legacy.Reporting` | the name is excused with `mapsTo: Reporting`; every dependency rule still applies to the project |
| Type rules | `typeNaming`, `typeShape`, `typeNamespace`, `attributePolicy` | interfaces are `I`-prefixed, classes are sealed, collections are read-only, every type sits under its project's `RootNamespace`, and no `[Obsolete]` lacks a message |
| Contracts | `apiSurface`, `apiDocumentation` | the public API matches `.config/api-surface.json`; every public type has XML documentation |
| Ports | `portImplementation`, `portPurity` | `IOrderRepository` has two public implementations and names no infrastructure technology |
| Adoption | `dependencyBudget` | one entry per production project; a new reference fails, and a stale allowance fails with `failOnStale` |

The sample's six projects compile to six assemblies, so the type-level and contract readers have
real metadata to inspect.

## Run it

```bash
dotnet test src/samples/ArchitectureSample/tests/Sample.ArchitectureTests/Sample.ArchitectureTests.csproj
```

With the tool installed, the same policy outside TUnit:

```bash
dotnet tool install --global Purview.Architecture.Testing.Cli

architests validate   --repository src/samples/ArchitectureSample
architests api check  --repository src/samples/ArchitectureSample
architests waivers list --repository src/samples/ArchitectureSample
architests graph      --repository src/samples/ArchitectureSample
architests graph --format mermaid --repository src/samples/ArchitectureSample
```

## See a rule fire

Each change below breaks exactly one rule; run the suite, read the message, then undo it.

1. **Dependency direction.** Add `<ProjectReference Include="../Persistence.InMemory/Persistence.InMemory.csproj" />`
   to `Domain.csproj`: `dependencyMatrix` reports the forbidden edge, and
   `assemblyDependencyMatrix` may report the type-level use too.
2. **Type shape.** Remove `sealed` from `InMemoryOrderRepository`: `typeShape` reports an undeclared
   public extension point.
3. **Port purity.** Add `Microsoft.EntityFrameworkCore.DbContext Context { get; }` to
   `IOrderRepository`: `portPurity` reports the infrastructure leak.
4. **Documentation.** Delete the `<summary>` from `OrderReport`: `apiDocumentation` reports the
   undocumented public type.
5. **API surface.** Add a public type to Domain: `apiSurface` reports it as an addition until you run
   `architests api capture` and commit the baseline.
6. **Dependency budget.** Add a reference from Application to `Persistence.Json`: the budget reports
   the reference as over budget.
7. **Waiver.** Remove the `Legacy.Reporting` waiver: `naming` reports the project name, because it
   maps to no responsibility.

## Regenerate the baseline

After an intended API change:

```bash
architests api capture --repository src/samples/ArchitectureSample
```

Commit `.config/api-surface.json` with the change and explain it in the pull request.
