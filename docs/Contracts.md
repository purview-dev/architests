# API contracts

Dependency rules say which project may reference which. The contract rules say what the public
surface actually **is**, and whether the interfaces that define a boundary are honest. They read the
compiled assemblies by metadata only and are off by default, because they need a build.

| Rule | Group | Catches |
| -- | -- | -- |
| `apiSurface` | `apiRules` | The public API differs from the committed baseline |
| `apiDocumentation` | `apiRules` | A public type or member has no XML documentation |
| `portImplementation` | `portRules` | A port in an inner layer has no implementation |
| `portPurity` | `portRules` | A port's signature names an infrastructure technology |

```json
"apiRules": {
  "enabled": true
},
"portRules": {
  "enabled": true
}
```

Each enabled rule is its own TUnit test. A disabled group produces no test, so an absent `apiSurface`
test means the group is off, not that the surface is unchecked.

## The committed baseline

`.config/api-surface.json` records every public type and member, grouped by project, with the
responsibility recorded for review context. It is the contract: a change to it is a change to what a
consumer may call.

- **Captured, never hand-written.** `architests api capture` writes it.
- **Canonical.** Projects, types, interfaces and members are sorted; there are no timestamps; line
  endings are always `\n`. An unchanged API regenerates byte for byte, so a diff means a real change.
- **Committed.** It is reviewed like code. A pull request that changes it explains why.

```bash
dotnet tool install --global Purview.Architecture.Testing.Cli

architests api capture     # write the baseline from the current build
architests api check       # compare; exit 1 on any difference
```

`api check` is the CI gate; `apiSurface` is the same comparison as a test. A missing baseline is
itself an `apiSurface` failure, because the rule would otherwise pass without checking anything.

`api capture` and `api check` accept `--repository <path>` and `--baseline <path>`. Unlike a test run,
a command scans the repository's own build output when `assemblyRules.assemblyRoots` is empty, because
a tool does not run from a test output directory.

## Reading a baseline failure

The message names the project, the type or member, and whether it was added, removed or changed.

- **Added.** The API grew. Run `architests api capture`, inspect the added entries, and commit them
  with the feature. Additions are not breaking, but they are still a reviewed contract change.
- **Removed.** The message calls it breaking. Restore the member, or capture the baseline as a
  deliberate breaking release and say so in the pull request.
- **Changed.** The shape moved: kind, `sealed`, `abstract`, base type, interfaces, a member's type, or
  whether it is static. Treat it like a removal until proven otherwise.

`api check --format sarif` emits SARIF 2.1.0 for a code-scanning annotation step: removals at
`error`, additions and changes at `warning`.

## API documentation

`apiDocumentation` reads the XML documentation file the compiler writes beside each assembly.

- `requireTypeDocumentation` (default true) requires a `<summary>` entry for every public type.
- `requireMemberDocumentation` (default false) extends that to every public member.

Member documentation is off by default because a record's positional parameters document its
properties through the type's entry; requiring a separate entry per generated property would punish
a correct record.

A project that ships no XML file is reported once with the fix: set
`<GenerateDocumentationFile>true</GenerateDocumentationFile>`. The Purview SDK does this already.

## Ports

A **port** is an interface in one of `portRules.categories`, which default to `domain` and
`application`: the layers that own contracts. An interface in `shared` or in an adapter is not a port
by this definition.

### `portImplementation`

Every port needs at least one **implementation** in a production project. An unimplemented port
is either dead or a boundary nobody crossed; both deserve a review. The dependency matrix still
governs where that implementation may live.

An `internal` adapter counts. Visibility does not change whether the boundary was crossed, and an
adapter only the composition root wires is commonly `internal` — as are generator-emitted ones. Set
`portRules.requirePublicImplementation` to `true` if the repository ships its adapters as public API
and wants that enforced.

An extension point implemented only by consumers (such as this package's `IArchitectureRule`) is not a
good fit: keep it out of the port categories, or record a narrow waiver.

### `portPurity`

A port's property types, return types and parameter types must not name a forbidden prefix. Defaults
cover `Microsoft.EntityFrameworkCore`, `System.Data`, `Microsoft.AspNetCore` and `System.Net.Http`.
Generic arguments count, so `IReadOnlyList<DbContext>` is caught.

```json
"portRules": {
  "enabled": true,
  "forbiddenTypePrefixes": ["Microsoft.EntityFrameworkCore", "System.Data"]
}
```

A port that names a database or an HTTP client is not a boundary; it is a leak. Move the technology
into the adapter and keep the interface in terms the domain understands.

## In the sample

[`ArchitectureSample`](https://github.com/purview-dev/architests/tree/main/src/samples/ArchitectureSample)
shows all four rules:

- `Domain.IOrderRepository` is the port, implemented by `Persistence.InMemory` and
  `Persistence.Json`;
- `Application.PlaceOrderHandler` depends on the port, never on an adapter;
- `Composition` selects the adapter;
- `.config/api-surface.json` is the committed baseline for all six projects.

## CI

```yaml
- run: architests api check
```

`api check` needs a build but no test run. In the test suite, `apiSurface` performs the same
comparison, so the command exists for a fast job and for a reviewer who wants the diff without
running TUnit.
