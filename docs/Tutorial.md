# Tutorial: grow a policy with the code

[Getting started](Getting-Started.md) gives you the smallest working setup. This tutorial goes further:
it builds one small application and grows its policy alongside it, from a two-project dependency rule
to the full set of assembly, type, API, port and budget checks.

Each stage:

1. adds a structural idea to the code and the matching policy;
2. ends with a green suite;
3. deliberately breaks one rule so you can read the failure and fix the cause.

Use a scratch repository, or follow along against
[`ArchitectureSample`](https://github.com/purview-dev/architests/tree/main/src/samples/ArchitectureSample).
The sample is the finished version of this tutorial, with the same project names and a complete
policy you can compare against.

## Before you start

You need a TUnit test project with:

```bash
dotnet add package Purview.Architecture.Testing
```

and the one-line suite:

```csharp
using Purview.Architecture.Testing;

[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests;
```

`[InheritsTests]` is required: TUnit does not walk base types during discovery, so without it the
class compiles, the run is green and nothing is enforced. The test project must reference every
production project, because the type-level rules read the compiled assemblies from its output. Add
each new production project to the test project as you create it; the later stages only see what is
in the test output.

The layout used here is:

```text
orders/
├── .config/
│   └── architecture-policy.json
├── src/
│   ├── Domain/
│   ├── Application/
│   ├── Persistence.InMemory/
│   ├── Persistence.Json/
│   ├── Persistence.SqlServer/
│   ├── Composition/
│   └── Legacy.Reporting/
└── tests/
    └── ArchitectureTests/
```

The stages add these projects as they need them. Every project name starts with a responsibility
name; the part after the first dot is an implementation qualifier and does not create a separate
responsibility.

## Stage 1: two projects and one direction

Start with the domain and one use case.

`src/Domain/Order.cs`:

```csharp
namespace Domain;

/// <summary>An order placed through the application.</summary>
/// <param name="Id">The order identifier.</param>
/// <param name="Total">The order total.</param>
public sealed record Order(Guid Id, decimal Total);
```

`src/Domain/IOrderRepository.cs`:

```csharp
namespace Domain;

/// <summary>The port through which orders are stored.</summary>
public interface IOrderRepository
{
    /// <summary>Stores an order.</summary>
    /// <param name="order">The order to store.</param>
    void Save(Order order);

    /// <summary>Returns every stored order.</summary>
    /// <returns>The stored orders.</returns>
    IReadOnlyList<Order> All();
}
```

`src/Application/PlaceOrderHandler.cs`:

```csharp
using Domain;

namespace Application;

/// <summary>Coordinates placing an order over the domain's port.</summary>
/// <param name="repository">Where placed orders are stored.</param>
public sealed class PlaceOrderHandler(IOrderRepository repository)
{
    /// <summary>Places an order and stores it.</summary>
    /// <param name="total">The order total.</param>
    /// <returns>The placed order.</returns>
    public Order Place(decimal total)
    {
        Order order = new(Guid.NewGuid(), total);
        repository.Save(order);
        return order;
    }
}
```

`Application.csproj` references `Domain`; `Domain.csproj` references nothing.

### The policy

Create `.config/architecture-policy.json`:

```json
{
  "$schema": "https://raw.githubusercontent.com/purview-dev/architests/main/architecture-policy.schema.json",
  "version": 3,
  "sourceRoots": ["src"],
  "responsibilities": [
    {
      "name": "Domain",
      "category": "domain",
      "purpose": "The order model, its invariants and the storage port it owns.",
      "mayDependOn": [],
      "mustNotDependOn": ["Application"]
    },
    {
      "name": "Application",
      "category": "application",
      "purpose": "Coordinates the use cases over the domain's ports.",
      "mayDependOn": ["Domain"],
      "mustNotDependOn": []
    }
  ]
}
```

What each field means here:

| Field | Why it is there |
| -- | -- |
| `sourceRoots` | Where the project graph is discovered. An empty graph would satisfy every rule, which is why `graphDiscovery` fails instead of passing silently. |
| `name` | The responsibility's first name segment. `Application` classifies the `Application` project and any `Application.*` implementation. |
| `category` | What kind of box this is. It matters later for ports, composition and hosts. |
| `purpose` | Why the responsibility exists. It is quoted in failure messages and must be at least 20 characters. |
| `mayDependOn` | The **complete** set of permitted references. An empty array means "depends on nothing"; silence is not permission. |
| `mustNotDependOn` | Prohibitions worth naming. They must not overlap `mayDependOn`. |

Run the suite:

```bash
dotnet test
```

The default suite now checks the declared project graph: `policyIntegrity`, `graphDiscovery`,
`naming`, `dependencyMatrix`, `cycle`, `testReference`, `discouragedName`, `forbiddenPackage`,
`forbiddenFramework`, `compositionBoundary`, `siblingIndependence` and `waiverIntegrity`. The
package, assembly, type, API, port and budget groups are still off.

### Break it: forget a permission

The code is correct, but the policy can still be wrong. Change `Application`'s permission to an empty
list:

```diff
-      "mayDependOn": ["Domain"],
+      "mayDependOn": [],
```

`dependencyMatrix` now reports that `Application` references `Domain`, "which the policy neither
permits nor forbids", and tells you to add `Domain` to `mayDependOn`. That is the allowlist doing its
job: an unlisted edge is a failure, not an accidental allowance. Restore `["Domain"]`.

`mustNotDependOn` is for the prohibitions worth naming. `Domain` could not reference `Application`
even without it, because `Domain.mayDependOn` is empty; adding the prohibition makes the intent
explicit and gives the stronger "must not depend" message.

## Stage 2: an adapter, and a port in the middle

The application depends on `IOrderRepository`, but nothing implements it yet. Add
`src/Persistence.InMemory` and reference `Domain` only.

`src/Persistence.InMemory/InMemoryOrderRepository.cs`:

```csharp
using Domain;

namespace Persistence.InMemory;

/// <summary>An in-memory adapter for the domain's storage port.</summary>
public sealed class InMemoryOrderRepository : IOrderRepository
{
    readonly List<Order> _orders = [];

    /// <inheritdoc />
    public void Save(Order order) => _orders.Add(order);

    /// <inheritdoc />
    public IReadOnlyList<Order> All() => _orders;
}
```

Add the responsibility and extend the two prohibitions:

```json
{
  "name": "Persistence",
  "category": "infrastructure",
  "purpose": "Adapters that implement the domain's storage port.",
  "mayDependOn": ["Domain"],
  "mustNotDependOn": ["Application"]
}
```

```diff
-      "mustNotDependOn": ["Application"]
+      "mustNotDependOn": ["Application", "Persistence"]
```

on `Domain`, and:

```diff
-      "mustNotDependOn": []
+      "mustNotDependOn": ["Persistence"]
```

on `Application`.

`Application` does **not** reference an adapter. It owns the use case and depends on the port; the
adapter depends on the port too, and on nothing above it. The dependency arrow points inward:

```text
Application ──> Domain <── Persistence.InMemory
```

### Break it: let the adapter depend on the application

Add this to `Persistence.InMemory.csproj`:

```xml
<ProjectReference Include="..\Application\Application.csproj" />
```

`dependencyMatrix` reports:

```text
'Persistence.InMemory' (Persistence) references 'Application' (Application), but Persistence must
not depend on Application. Invert it: Application may depend on Persistence, or introduce an
abstraction that Persistence owns and Application implements.
```

Remove it. If you remove `Application` from `mustNotDependOn` but leave it out of `mayDependOn`, the
same rule reports that the policy "neither permits nor forbids" the edge. Both failures mean the same
thing: the policy is complete, so an unlisted edge is not accidentally allowed.

## Stage 3: siblings and the composition root

Add `src/Persistence.Json` as a second adapter for the same port, referencing `Domain` only:

```csharp
using System.Text.Json;
using Domain;

namespace Persistence.Json;

/// <summary>A JSON adapter for the domain's storage port.</summary>
public sealed class JsonOrderRepository : IOrderRepository
{
    readonly List<string> _documents = [];

    /// <inheritdoc />
    public void Save(Order order) => _documents.Add(JsonSerializer.Serialize(order));

    /// <inheritdoc />
    public IReadOnlyList<Order> All() =>
        [.. _documents.Select(document => JsonSerializer.Deserialize<Order>(document)!)];
}
```

Now add the composition root, which is allowed to know every layer:

`src/Composition/CompositionRoot.cs`:

```csharp
using Application;
using Domain;
using Persistence.InMemory;

namespace Composition;

/// <summary>Selects an adapter and wires the application handler.</summary>
public sealed class CompositionRoot
{
    readonly PlaceOrderHandler _handler;

    /// <summary>Wires the in-memory adapter into the handler.</summary>
    public CompositionRoot() => _handler = new PlaceOrderHandler(new InMemoryOrderRepository());

    /// <summary>Places an order through the wired adapter.</summary>
    /// <param name="total">The order total.</param>
    /// <returns>The placed order.</returns>
    public Order PlaceAndSave(decimal total) => _handler.Place(total);
}
```

`Composition.csproj` references `Domain`, `Application`, `Persistence.InMemory` and
`Persistence.Json`.

Add the responsibility, and turn on sibling independence for the qualified adapters:

```json
{
  "name": "Composition",
  "category": "composition",
  "purpose": "The composition root that selects an adapter and wires the handler.",
  "mayDependOn": ["Domain", "Application", "Persistence"],
  "mustNotDependOn": []
}
```

```diff
-      "mustNotDependOn": ["Application", "Persistence"]
+      "mustNotDependOn": ["Application", "Persistence", "Composition"]
```

on `Domain` gives the matrix an explicit prohibition for the new responsibility.

```diff
-      "mustNotDependOn": ["Persistence"]
+      "mustNotDependOn": ["Persistence", "Composition"]
```

on `Application`, and:

```diff
   "name": "Persistence",
   "category": "infrastructure",
   "purpose": "Adapters that implement the domain's storage port.",
   "mayDependOn": ["Domain"],
-  "mustNotDependOn": ["Application"]
+  "mustNotDependOn": ["Application", "Composition"],
+  "siblingIndependence": true
```

`category: "composition"` is what makes the composition boundary work. It is a category, not a
project name, so the rule still applies if the project is called `Host` or `Bootstrap`.

### Break it: make one adapter depend on its sibling

Add a reference from `Persistence.Json` to `Persistence.InMemory`:

```xml
<ProjectReference Include="..\Persistence.InMemory\Persistence.InMemory.csproj" />
```

`siblingIndependence` reports:

```text
'Persistence.Json' references its sibling 'Persistence.InMemory'. Sibling Persistence
implementations must stay independent; move the shared part into the unqualified 'Persistence'
project or into an abstraction both can depend on.
```

Remove the reference. A shared helper belongs in the shared `Persistence` project (if one exists), not
in one adapter that the other reaches into.

### The composition boundary

`compositionBoundary` fires when a project outside the `composition` and `host` categories references
the composition root. We will exercise it with `Legacy.Reporting` in stage 7: that project does not
take part in composition, so adding the reference cannot create a cycle. Using an adapter here would
create one, and the build would fail before the rule could report it.

## Stage 4: keep technology in the adapter

The adapters are still technology-neutral. Add a real database adapter, `Persistence.SqlServer`,
referencing `Domain` and the EF Core SQL Server package:

```xml
<ItemGroup>
  <ProjectReference Include="..\Domain\Domain.csproj" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="..." />
</ItemGroup>
```

Use the version your repository already has, or omit `Version` if central package management supplies
it. The adapter is the only project that names EF Core:

```csharp
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Persistence.SqlServer;

/// <summary>An EF Core adapter for the domain's storage port.</summary>
/// <param name="context">The database context.</param>
public sealed class SqlOrderRepository(DbContext context) : IOrderRepository
{
    /// <inheritdoc />
    public void Save(Order order)
    {
        context.Set<Order>().Add(order);
        context.SaveChanges();
    }

    /// <inheritdoc />
    public IReadOnlyList<Order> All() => [.. context.Set<Order>()];
}
```

Add `Persistence.SqlServer` to `Composition.csproj` as well. The composition root still selects the
in-memory adapter in this tutorial; the reference exists so you can see what happens to the package
graph.

Now tell `Domain` and `Application` that this technology is not theirs:

```jsonc
// Add both properties to the Domain and Application responsibilities.
"forbiddenPackagePrefixes": ["Microsoft.EntityFrameworkCore"],
"forbiddenFrameworkReferences": ["Microsoft.AspNetCore.App"]
```

Add those two lines to both responsibility objects. `forbiddenPackage` defaults to
`forbiddenPackageScope: "own"`:

- a package the project declares itself is owned;
- a package injected by `Directory.Build.props` is owned;
- a package its own package references pull in is owned;
- a package inherited through a **permitted** project reference is not.

That last point is why `Composition` may reference `Persistence.SqlServer` without being blamed for
EF Core. It chose the adapter; the adapter chose the database technology. Set
`"forbiddenPackageScope": "closure"` only when the policy really means "this technology must not be
anywhere on the compile graph, even inherited". That stricter mode usually requires adapters to mark
implementation packages `PrivateAssets="compile"`.

### Break it: declare EF Core in the domain

Add the package to `Domain.csproj`:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="..." />
```

`forbiddenPackage` reports:

```text
'Domain' (Domain) declares or brings in package 'Microsoft.EntityFrameworkCore' through its own
package references, which matches the forbidden prefix 'Microsoft.EntityFrameworkCore'. No permitted
project reference explains it.
```

Remove it. The same failure appears if the package arrives transitively from one of `Domain`'s own
package references, or is injected by a shared props file: ownership is about the project's choices,
not only its `.csproj` lines.

### Break it: take a framework reference in the application

Add this to `Application.csproj`:

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
</ItemGroup>
```

`forbiddenFramework` reports:

```text
'Application' (Application) references framework 'Microsoft.AspNetCore.App', which matches the
forbidden prefix 'Microsoft.AspNetCore.App' for Application. Coordinates the use cases over the
domain's ports. Move the framework reference to the responsibility that owns that technology.
```

Remove it. A host may need the web framework; a portable use-case layer should not.

## Stage 5: read what the compiler sees

The project matrix only sees declared `ProjectReference`s. Enable the assembly reader and the type
rules:

```jsonc
// Add these top-level properties beside "responsibilities".
"assemblyRules": {
  "enabled": true,
  "assemblyRoots": []
},
"typeRules": {
  "enabled": true,
  "appliesTo": {
    "categories": ["domain", "application", "infrastructure", "shared"]
  },
  "excludeGeneratedCode": true,
  "sealConcreteClasses": true
}
```

An empty `assemblyRoots` scans the test output directory, which already contains every production
assembly the test project references. The reader uses metadata only: it never loads or executes an
assembly. `assemblyDependencyMatrix` now applies the **same** `mayDependOn` / `mustNotDependOn`
matrix to every `TypeReference`, so a type that arrives through another project is checked even when
the `.csproj` does not name the ultimate dependency.

`appliesTo.categories` limits which categories are governed; `excludeGeneratedCode` skips
`[GeneratedCode]` and `[CompilerGenerated]` types; `sealConcreteClasses` is one of the shape checks.
The type rules inspect public declarations in the governed categories:

| Rule | What this stage now enforces |
| -- | -- |
| `typeNaming` | Interfaces start with `I`; attributes and exceptions carry their suffixes; no `Manager`/`Helper`-style names |
| `typeShape` | Public concrete classes are sealed, fields are private, collection returns are read-only, utility classes are static |
| `typeNamespace` | A public type sits under its project's `RootNamespace` (the project name by default) |
| `attributePolicy` | `[Obsolete]` has a message, and required or forbidden attributes are honoured |

### Break it: unseal the model

Remove `sealed` from `Order`:

```csharp
public record Order(Guid Id, decimal Total);
```

`typeShape` reports:

```text
'Domain.Order' is a public, inheritable class. Seal it, or make it abstract if it is meant to be a
base type: an unsealed class is a public extension point nobody declared.
```

Put `sealed` back. A record is a class to the metadata reader, so the same rule applies.

### Break it: name an interface like a class

Add a public interface beside the port:

```csharp
public interface OrderStore
{
}
```

`typeNaming` reports:

```text
'Domain.OrderStore' is an interface whose name does not start with 'I'. Rename it to 'IOrderStore'
so the kind is visible at every use.
```

Delete it. The rule is about making the kind visible at every use, not about a naming preference in
one file.

### Break it: expose a mutable collection

Change the port's `All` return type:

```csharp
List<Order> All();
```

and update the implementations. `typeShape` reports:

```text
'Domain.IOrderRepository.All' exposes 'System.Collections.Generic.List<Domain.Order>'. Return
IReadOnlyList<T> or IReadOnlyDictionary<TKey, TValue> so a caller cannot mutate the owner's state.
```

Restore `IReadOnlyList<Order>`. The same rule catches a public `List<T>` property on a class. If a
type is deliberately outside the governed namespace, fix the namespace or set `RootNamespace`
explicitly; silently excluding it is how a public API grows unwatched.

### What the assembly rule adds

Suppose `Application` references `Domain`, and `Domain` accidentally references something that in
turn exposes a persistence type. Even if `Domain.csproj` never names the persistence project,
`assemblyDependencyMatrix` sees the `TypeReference` to the persistence assembly and applies the
matrix. A package assembly is not attributed to a project, so it is ignored by this rule; package
policy belongs to `forbiddenPackage`.

## Stage 6: contracts and ports

The public API is now worth protecting. Enable the two contract groups:

```jsonc
// Add these top-level properties beside "responsibilities".
"apiRules": {
  "enabled": true
},
"portRules": {
  "enabled": true
}
```

`apiRules` reads the compiled assemblies and compares their public surface with
`.config/api-surface.json`. Additions and removals are both reported: the baseline is the reviewed
contract, so growing it is deliberate. A missing baseline is itself a failure, because an API rule
that silently checks nothing is worse than no rule.

`portRules` treats interfaces in the `domain` and `application` categories as ports. It reports a port
with no public implementation, and a port whose signature names an infrastructure technology. The
default forbidden prefixes are `Microsoft.EntityFrameworkCore`, `System.Data`, `Microsoft.AspNetCore`
and `System.Net.Http`.

Capture the baseline once, before enforcing it:

```bash
dotnet tool install --global Purview.Architecture.Testing.Cli
architests api capture
git add .config/api-surface.json
```

Then run the suite. `apiDocumentation` requires XML documentation on every public type and member;
the code in this tutorial already has summaries, so the first run should be clean. If it lists
missing documentation, add the `<summary>` elements until it is green.

### Break it: add an undocumented public type

Add:

```csharp
public sealed record OrderSummary(Guid Id, decimal Total);
```

`apiDocumentation` reports the new type. Add:

```csharp
/// <summary>A read-only summary of an order.</summary>
/// <param name="Id">The order identifier.</param>
/// <param name="Total">The order total.</param>
```

and the rule is satisfied. The type is an API addition, so `apiSurface` will also report it until you
capture the baseline deliberately.

### Break it: change the contract

Add a property to `Order`:

```csharp
public string Status { get; init; } = "Placed";
```

`apiSurface` reports the addition. If the change is intended, run `architests api capture`, review the
diff in `.config/api-surface.json`, and commit it with the code. If it is not intended, revert it.
That is the whole point of the baseline: the API cannot change by accident.

### Break it: add a port nobody implements

Add a public interface to `Domain`:

```csharp
/// <summary>Notifies a recipient that an order was placed.</summary>
public interface IOrderNotifier
{
    /// <summary>Notifies about an order.</summary>
    /// <param name="order">The order.</param>
    void Notify(Order order);
}
```

`portImplementation` reports:

```text
'Domain.IOrderNotifier' is a port in Domain with no public implementation in the repository.
Implement it in an adapter, or remove it: an unimplemented contract is a boundary nobody crossed.
```

Delete it. The rule asks whether the port is implemented anywhere in the repository; the dependency
matrix still decides where an implementation may live. The new interface is also a public API change,
so `apiSurface` reports it until you either remove it or capture a deliberate baseline.

### Break it: leak a technology into the port

Add this member to `IOrderRepository`:

```csharp
System.Data.Common.DbConnection Connection { get; }
```

`portPurity` reports:

```text
'Domain.IOrderRepository.Connection' exposes 'System.Data.Common.DbConnection', which names the
infrastructure prefix 'System.Data'. A port must be technology-neutral; move the technology into
the adapter.
```

Remove the member. A signature is part of the boundary: even a type the domain can reference without
a new package still tells every adapter what technology to use.

## Stage 7: waivers and the ratchet

Real repositories contain history. Add a reporting project whose name predates the catalogue:

`src/Legacy.Reporting/Legacy.Reporting.csproj` references `Domain`, and
`OrderReport.cs` is a small read-only projection:

```csharp
using Domain;

namespace Legacy.Reporting;

/// <summary>Read-only totals for the legacy report.</summary>
public sealed class OrderReport
{
    /// <summary>Returns the total value of the given orders.</summary>
    /// <param name="orders">The orders to total.</param>
    /// <returns>The sum of the order totals.</returns>
    public decimal Total(IReadOnlyList<Order> orders) => orders.Sum(order => order.Total);
}
```

Add the responsibility it belongs to:

```json
{
  "name": "Reporting",
  "category": "shared",
  "purpose": "Read-only projections over the domain for the legacy report.",
  "mayDependOn": ["Domain"],
  "mustNotDependOn": ["Application", "Persistence", "Composition"]
}
```

```diff
-      "mustNotDependOn": ["Application", "Persistence", "Composition"]
+      "mustNotDependOn": ["Application", "Persistence", "Composition", "Reporting"]
```

on `Domain` keeps the prohibitions symmetrical.

`naming` now fails for `Legacy.Reporting`, because its first segment is not a responsibility. Renaming
is the cleaner fix; when a published pipeline depends on the name, record a waiver instead:

```jsonc
// Add these top-level properties beside "responsibilities".
"waiverPolicy": {
  "maxAgeDays": 180
},
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

Use the date you actually review it; `maxAgeDays` makes an old waiver a `waiverIntegrity` failure.
`mapsTo` keeps every dependency rule in force: the waiver excuses only the name, and
`Legacy.Reporting` must still obey `Reporting`'s matrix. `SkipRule` would switch the rule off for the
whole repository and leave no record, so it is not the adoption tool.

The new public type changes the API. Capture it deliberately:

```bash
architests api capture
```

Review the diff and commit the baseline with the project.

### Break it: reference the composition root

Add a reference from `Legacy.Reporting` to `Composition`:

```xml
<ProjectReference Include="..\Composition\Composition.csproj" />
```

Two rules fire without creating a project cycle:

- `dependencyMatrix` — `Reporting` has `Composition` in `mustNotDependOn`.
- `compositionBoundary` — `Composition` is the composition root, and only a `host` or another
  `composition` project may reference it.

```text
'Legacy.Reporting' references the composition root. Composition selects implementations, so it
depends on them - never the other way round, which would make the choice circular.
```

Remove the reference. The composition root is the one place that chooses an adapter; if a consumer
could call back into it, the choice would be circular.

### Ratchet the project graph

A dependency budget records the references each project may declare today. Enable it with one entry
per project:

```jsonc
// Add this top-level property beside "responsibilities".
"dependencyBudget": {
  "enabled": true,
  "failOnStale": true,
  "entries": [
    { "project": "Domain", "references": [] },
    { "project": "Application", "references": ["Domain"] },
    { "project": "Persistence.InMemory", "references": ["Domain"] },
    { "project": "Persistence.Json", "references": ["Domain"] },
    { "project": "Persistence.SqlServer", "references": ["Domain"] },
    {
      "project": "Composition",
      "references": [
        "Domain",
        "Application",
        "Persistence.InMemory",
        "Persistence.Json",
        "Persistence.SqlServer"
      ]
    },
    { "project": "Legacy.Reporting", "references": ["Domain"] }
  ]
}
```

Turn on the reachability check once the matrix is honest:

```jsonc
// Add this top-level property beside "responsibilities".
"policyIntegrity": {
  "checkReachability": true
}
```

It reports a prohibition that a permitted path can bypass — for example, if `Domain` may depend on
`Application`, and `Application` may depend on `Persistence`, then a prohibition on `Domain` reaching
`Persistence` is not real. Fix the matrix, not the rule.

### Break it: add an unbudgeted reference

Add `Application` → `Persistence.InMemory`:

```xml
<ProjectReference Include="..\Persistence.InMemory\Persistence.InMemory.csproj" />
```

Two independent rules fail:

- `dependencyMatrix` reports the forbidden edge.
- `dependencyBudget` reports that the reference is not in `Application`'s budget.

Remove the reference. The budget catches a reference the matrix might permit but nobody reviewed;
the matrix catches a direction the architecture forbids. Both are reviewed policy changes.

### Break it: leave a stale allowance

Remove `Persistence.Json` from `Composition.csproj`. The project still compiles, but
`dependencyBudget` fails:

```text
The dependency budget for 'Composition' still allows 'Persistence.Json', but the project no longer
references it. Tighten the budget: a stale allowance hides the next dependency.
```

Either restore the reference or remove the allowance in the same reviewed change. `dependencyBudget`
is deliberately not waivable: the budget is the record that keeps dependency growth reviewable.

## Run the finished policy

The suite is now the whole architecture review:

```bash
dotnet test
```

Outside TUnit, the same policy can be checked from a terminal:

```bash
architests validate                 # schema and policy-integrity checks
architests waivers list --stale     # waivers that need review
architests api check                # public API against the committed baseline
architests graph                    # the project graph
architests graph --format mermaid   # the same graph for a diagram
```

`architests validate` does not run the project, assembly or type rules; `dotnet test` does. Keep both:
the test suite is the enforcement, and the CLI is the cheap check for hooks and CI steps.

## What each stage added

| Stage | Code change | Policy idea | Rules that start doing work |
| -- | -- | -- | -- |
| 1 | Domain + Application | A complete allowlist, with an explicit prohibition | `dependencyMatrix`, `naming`, `graphDiscovery` |
| 2 | An in-memory adapter | A port is implemented from the outside | `dependencyMatrix` in the other direction |
| 3 | A second adapter + composition root | Sibling independence and a composition category | `siblingIndependence`, `compositionBoundary` |
| 4 | An EF Core adapter | Technology belongs to the adapter, not the inner layers | `forbiddenPackage`, `forbiddenFramework` |
| 5 | Metadata reader + type rules | The compiled graph is the real dependency graph | `assemblyDependencyMatrix`, `typeNaming`, `typeShape`, `typeNamespace`, `attributePolicy` |
| 6 | API baseline + port rules | The public surface is a reviewed contract; a port stays neutral | `apiSurface`, `apiDocumentation`, `portImplementation`, `portPurity` |
| 7 | Legacy project + budget | Exceptions are recorded and reviewed; growth is a ratchet | `waiverIntegrity`, `dependencyBudget`, reachability |

The order matters less than the habit: add one idea, make the suite green, then ratchet. A policy that
describes a perfect architecture on day one gets switched off; a policy that describes today's
architecture and tightens one edge at a time survives.

## Where to go next

- [The policy reference](Policy-Reference.md) — every field, default and rule id.
- [Type-level rules](Type-Level-Rules.md) — how the assembly reader attributes a type to a project.
- [API contracts](Contracts.md) — the baseline, XML documentation and the port rules.
- [Migration and the budget](Migration.md) — adopting this in a repository that already has history.
- [Command line](Cli.md) — `validate`, `api capture`, `waivers list` and `graph`.
- [Extending](Extending.md) — a bespoke rule when the catalogue does not fit.
- [The sample](https://github.com/purview-dev/architests/tree/main/src/samples/ArchitectureSample) —
  the completed version of this application, with its full policy and baseline.
