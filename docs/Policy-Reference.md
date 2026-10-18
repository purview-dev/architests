# Policy reference

The policy is `.config/architecture-policy.json`. It is validated against the schema embedded in the
package before any rule runs, so a field the reader understands but the schema rejects is a bug.

The authoritative definition is
[`architecture-policy.schema.json`](https://github.com/purview-dev/architests/blob/main/architecture-policy.schema.json).

## Top-level fields

| Field | Type | Default | Meaning |
| -- | -- | -- | -- |
| `$schema` | string | — | Pointer to the published schema, so an editor validates as you type. |
| `version` | integer ≥ 1 | — | The document version. The current version is **3**. |
| `description` | string | — | Free text. |
| `conventions` | object | — | `qualifiedNameSeparator` and `dependencyArrowMeaning`, for humans and editors. |
| `sourceRoots` | string[] | `["src"]` | Repo-relative folders holding the projects. An empty array scans the repository root. |
| `responsibilities` | object[] | — | **Required**, at least one. The responsibility catalogue. |
| `discouragedProjectNames` | object | — | Names that describe a category, with a `reason`. |
| `assemblyRules` | object | — | The type-level check. Off unless `enabled` is true. |
| `typeRules` | object | — | Type naming, shape, namespace and attribute rules. Off unless `enabled` is true. |
| `apiRules` | object | — | API baseline compatibility and XML documentation. Off unless `enabled` is true. |
| `portRules` | object | — | Port implementation and technology purity. Off unless `enabled` is true. |
| `dependencyBudget` | object | — | A ratchet on each project's declared references. Off unless `enabled` is true. |
| `policyIntegrity` | object | — | The policy's own consistency checks. `checkReachability` is off by default. |
| `waiverPolicy` | object | — | How long a waiver may go unreviewed. `maxAgeDays` is unset by default. |
| `waivers` | object[] | `[]` | Deliberate, reviewed departures. |

### `version` 3

Version 3 adds `typeRules`, `policyIntegrity` and `waiverPolicy`. All are optional, so a version-2
policy keeps passing unchanged; a repository adopts the new checks by adding the section it wants.

### `version` 2

Version 2 removed the `rules` object (`noCycles`, `noProductionToTestReferences`,
`implementationsMustNotReferenceComposition`, `siblingImplementationsIndependent`), whose flags were
deserialised but never consulted. Use `SkipRule` or a waiver to opt out of a rule. A version-1 policy
that set `rules` must delete the block; a version-1 policy that did not still validates.

## Responsibilities

| Field | Type | Required | Meaning |
| -- | -- | -- | -- |
| `name` | string | ✅ | A single PascalCase segment, with no dots. |
| `aliases` | string[] | | Recognised alternative names; an established project using one is not renamed. |
| `category` | enum | ✅ | `domain`, `application`, `infrastructure`, `composition`, `host` or `shared`. |
| `purpose` | string ≥ 20 | ✅ | Why the responsibility exists; quoted in failure messages. |
| `mayDependOn` | string[] | ✅ | Responsibilities this one may reference. An empty array means it depends on nothing. |
| `mustNotDependOn` | string[] | ✅ | Responsibilities this one must never reference. |
| `forbiddenPackagePrefixes` | string[] | | NuGet identifier prefixes that must not appear, e.g. `Microsoft.EntityFrameworkCore`. |
| `forbiddenFrameworkReferences` | string[] | | Framework reference prefixes that must not appear, e.g. `Microsoft.AspNetCore.App`. |
| `siblingIndependence` | boolean | | When true, qualified implementations of this responsibility must not reference one another. |
| `separateProjectWhen` | string ≥ 10 | | Documentation only. |
| `mayRemainCombinedWhen` | string ≥ 10 | | Documentation only. |

## Assembly rules

| Field | Type | Default | Meaning |
| -- | -- | -- | -- |
| `enabled` | boolean | `false` | Whether the type-level check runs. It reads compiled assemblies, so it needs a build to have produced them. |
| `assemblyRoots` | string[] | `[]` | Repo-relative folders to scan. An empty array scans the test output directory. |
| `assemblyGlobs` | string[] | `["*.dll"]` | File globs matched under each root. |

### Package resolution

`forbiddenPackage` checks two sets: the `PackageReference` elements declared in the project file, and
the **resolved closure** recorded by restore in `obj/project.assets.json`. The second catches a
package injected by a shared `Directory.Build.props` and one that only arrives transitively, neither
of which appears in the `.csproj`. The closure is present after `dotnet restore`; before it, the rule
falls back to the declared references.

## Type rules

`typeRules` reads each project's compiled assembly by metadata only — nothing is loaded or executed —
and polices the **public** surface it declares. Internal types are not checked: they are not part of
the API a consumer can call. The group is off by default because it needs a build.

| Field | Type | Default | Meaning |
| -- | -- | -- | -- |
| `enabled` | boolean | `false` | Whether the type-level rules run. |
| `appliesTo.categories` | string[] | `[]` | Categories to govern. Empty governs every production responsibility. |
| `excludeGeneratedCode` | boolean | `true` | Skip `[GeneratedCode]` and `[CompilerGenerated]` types. |
| `excludeNamespacePrefixes` | string[] | `[]` | Namespace prefixes to skip, such as a generated client. |
| `discouragedTypeSuffixes` | string[] | `["Manager", "Helper", "Util", "Impl"]` | Suffixes that name a mechanism rather than a responsibility. |
| `sealConcreteClasses` | boolean | `true` | Require a public concrete class to be sealed. |
| `requireReadOnlyCollections` | boolean | `true` | Reject `List<T>` and `Dictionary<TKey, TValue>` in a public property or return type. |
| `forbiddenAttributes` | string[] | `[]` | Attributes no governed public type may carry. |
| `requiredAttributes` | string[] | `[]` | Attributes every governed public type must carry. |
| `allowObsoleteWithoutMessage` | boolean | `false` | Allow `[Obsolete]` with no replacement message. |
| `forbidInternalsVisibleToProduction` | boolean | `true` | Reject `InternalsVisibleTo` naming a production assembly. |

```json
"typeRules": {
  "enabled": true,
  "appliesTo": { "categories": ["domain", "application", "shared"] },
  "excludeGeneratedCode": true
}
```

### Type naming

`typeNaming` reports a public interface that does not start with `I`, an attribute class that does not
end with `Attribute`, an exception class that does not end with `Exception`, and a type ending with a
configured discouraged suffix. The suffix list targets names that describe a mechanism — `Manager`,
`Helper`, `Util`, `Impl` — rather than the responsibility the type has.

### Type shape

`typeShape` reports:

- a public concrete class that is neither sealed nor abstract — an undeclared extension point;
- a public **instance** field, which cannot validate or evolve without a breaking change;
- a public property or method returning `List<T>` or `Dictionary<TKey, TValue>`, which hands the
  caller the owner's mutable state;
- a class named `...Exception` that does not derive from `System.Exception`;
- a class with only static members and no public constructor that is not declared `static`.

### Type namespace

`typeNamespace` compares each public type's declared namespace with the project's `RootNamespace`,
read from the `.csproj`. A type must be in that namespace or a sub-namespace. Metadata records the
namespace but not the file path, so the rule deliberately makes no claim about folders. A project
that has not set `RootNamespace` falls back to its project name; set it explicitly if the namespace
differs.

### Attribute policy

`attributePolicy` applies the policy's attribute lists to public types and reports:

- a type missing one of `requiredAttributes`;
- a type carrying one of `forbiddenAttributes`;
- `[Obsolete]` without a message on a public type or member, unless
  `allowObsoleteWithoutMessage` is true — an obsolete member with no replacement is a dead end;
- an assembly-level `InternalsVisibleTo` that names a **production** assembly. Exposing internals to
  a test project remains ordinary; exposing them to production code is a boundary leak.

## API rules

`apiRules` compares the public API a build produced with a committed baseline. The baseline is the
reviewed contract, so growing the API is as deliberate as shrinking it. Off by default because it
needs a build.

| Field | Type | Default | Meaning |
| -- | -- | -- | -- |
| `enabled` | boolean | `false` | Whether the API rules run. |
| `baseline` | string | `.config/api-surface.json` | The baseline path relative to the repository root. |
| `requireTypeDocumentation` | boolean | `true` | Require every public type to have an XML documentation entry. |
| `requireMemberDocumentation` | boolean | `false` | Require every public member to have one too. |

```json
"apiRules": {
  "enabled": true
}
```

### API surface

`apiSurface` reports every difference from the baseline: an added type or member, a removed one, or
a changed shape (kind, sealed, abstract, base type, interfaces, member type or static). A removal is
called out as breaking in the message.

The baseline is written by the tool, never by hand:

```bash
architests api capture   # write .config/api-surface.json
architests api check     # compare, exit 1 on any difference
```

The document is canonical: projects, types, interfaces and members are sorted, there are no
timestamps, and line endings are always `\n`, so an unchanged API regenerates byte for byte. Commit
it with the change that alters the API, exactly as you would commit a public API review.

A missing baseline is itself an `apiSurface` failure, because the rule would otherwise pass without
checking anything.

### API documentation

`apiDocumentation` reads the XML documentation file the compiler writes beside each assembly. Every
public type must have a `<summary>`; with `requireMemberDocumentation`, every public member must too.
A project that ships no XML file is reported once, with the fix: set
`<GenerateDocumentationFile>true</GenerateDocumentationFile>`.

Member documentation is off by default because a record's positional parameters document its
properties through the type's entry, so requiring a separate entry for every generated property
would penalise a correct record.

## Port rules

`portRules` treats interfaces in the inner layers as ports: contracts an outer layer implements.
Off by default because it reads compiled assemblies.

| Field | Type | Default | Meaning |
| -- | -- | -- | -- |
| `enabled` | boolean | `false` | Whether the port rules run. |
| `categories` | string[] | `["domain", "application"]` | The categories whose interfaces are ports. |
| `forbiddenTypePrefixes` | string[] | common infrastructure prefixes | Types a port's signature must not name. |

### Port implementation

`portImplementation` reports an interface in a port category with **no public implementation**
anywhere in the repository. An unimplemented contract is either dead or a boundary nobody crossed;
both are worth a review. A port implemented in any production project passes — the dependency matrix
still governs where that implementation may live.

### Port purity

`portPurity` reports a port whose property, method return, or parameter names a forbidden prefix,
such as `Microsoft.EntityFrameworkCore`, `System.Data`, `Microsoft.AspNetCore` or `System.Net.Http`.
A port that names a database or an HTTP client is not a boundary; it is a leak. Generic arguments
are part of the signature, so `IReadOnlyList<DbContext>` is caught too.

## Dependency budget

`dependencyBudget` is a ratchet on the project graph. It records the references each project is
allowed to declare today, so a new dependency cannot arrive without a reviewed policy change.

| Field | Type | Default | Meaning |
| -- | -- | -- | -- |
| `enabled` | boolean | `false` | Whether the budget is enforced. |
| `failOnStale` | boolean | `true` | Fail when an allowed reference no longer exists. |
| `entries[].project` | string | — | The production project the entry budgets. |
| `entries[].references` | string[] | — | The project references the budget permits. |

```json
"dependencyBudget": {
  "enabled": true,
  "failOnStale": true,
  "entries": [
    { "project": "Application", "references": ["Domain"] },
    { "project": "Persistence", "references": ["Domain", "Application"] }
  ]
}
```

The rule is **not waivable**: the budget is the record that keeps growth reviewable, and excusing it
would make the policy unfalsifiable.

- A reference not in the entry is a violation. Remove it, or add it in a reviewed change.
- With `failOnStale`, an allowance the project no longer uses is also a violation. Tighten the
  budget: a stale allowance hides the next dependency.
- Each production project has at most one entry. A project with no entry is not budgeted, so a
  repository can adopt the ratchet one project at a time.
- An empty `entries` array is valid and checks nothing, which is the recommended starting point: the
  policy is enabled, and the first entry is added deliberately.

## Policy integrity

`policyIntegrity` always runs, and is not waivable: a waiver excusing a broken policy would make the
policy unfalsifiable. It reports what a JSON Schema cannot express.

| Check | Reported |
| -- | -- |
| Unknown name | A `mayDependOn` or `mustNotDependOn` entry names no responsibility. Aliases and project names are not accepted in the matrix. |
| Overlap | The same responsibility appears in both lists for one responsibility. |
| Duplicate name | Two responsibilities share a name, so only the first can classify a project. |
| Alias collision | An alias equals a responsibility name, or two responsibilities declare the same alias. |
| Reachability (opt-in) | The permitted `mayDependOn` closure reaches a responsibility the policy forbids, so the prohibition cannot be enforced. |

```json
"policyIntegrity": {
  "checkReachability": true
}
```

The reachability failure names the path, for example `Core -> Application -> Persistence`, so the
edge that closes it is obvious. It is opt-in because a matrix can be perfectly consistent while a
longer permitted path still defeats a prohibition — a stronger claim, and a deliberate one.

## Waiver expiry

`waiverPolicy.maxAgeDays` gives `reviewedOn` a deadline:

```json
"waiverPolicy": {
  "maxAgeDays": 180
}
```

A waiver reviewed longer ago than the maximum is a `waiverIntegrity` failure. A waiver with no
`reviewedOn`, a malformed date, or a date in the future is also reported: an undated waiver cannot
expire, and a review cannot predate the departure it records. Expiry is off by default, so adopting
the maximum is a deliberate policy change rather than a breaking upgrade.

`architests waivers list --stale` applies the same review and exits non-zero when any waiver is due,
so the check also works in CI without a test run.

## Waivers

| Field | Type | Required | Meaning |
| -- | -- | -- | -- |
| `project` | string | ✅ | The project the waiver applies to, exactly as it appears on disk. |
| `mapsTo` | string | | The responsibility the project actually has. Keeps every dependency rule in force while excusing the name. |
| `rules` | string[] | ✅ | The rule ids this waiver excuses. At least one. |
| `reason` | string ≥ 40 | ✅ | Why the departure is acceptable. |
| `reviewedOn` | date | | When the departure was last reviewed, as `yyyy-MM-dd`. Checked against `waiverPolicy.maxAgeDays` when that is set. |

Any rule id may be waived, including `siblingIndependence` and `assemblyDependencyMatrix`. An unknown
id is a `waiverIntegrity` failure, so a typo cannot silently excuse nothing.

## Rules

| Rule id | Catches |
| -- | -- |
| `policyIntegrity` | The policy names an unknown responsibility, permits and forbids the same dependency, or declares a duplicate name or alias. Not waivable. |
| `graphDiscovery` | No production projects were discovered, so every other rule would pass vacuously. Not waivable. |
| `naming` | A production project whose name maps to no responsibility or alias. |
| `discouragedName` | A project named after a category, so nothing can be kept out of it. |
| `dependencyMatrix` | A declared `ProjectReference` the matrix forbids or does not permit. |
| `assemblyDependencyMatrix` | A type reference the matrix forbids or does not permit, including one that arrives transitively or through a package. |
| `typeNaming` | A public interface whose name lacks `I`, an attribute or exception without its suffix, or a discouraged suffix. |
| `typeShape` | An unsealed public class, a public instance field, a mutable collection return, or a utility class that is not static. |
| `typeNamespace` | A public type declared outside its project's root namespace. |
| `attributePolicy` | A required attribute missing, a forbidden one present, `[Obsolete]` without a message, or `InternalsVisibleTo` naming production code. |
| `apiSurface` | The public API differs from its committed baseline. |
| `apiDocumentation` | A public type or member has no XML documentation entry. |
| `portImplementation` | An interface in a port category has no public implementation. |
| `portPurity` | A port's signature exposes a forbidden infrastructure technology. |
| `dependencyBudget` | A project references beyond its budget, or the budget still allows a hook that has gone. Not waivable. |
| `siblingIndependence` | A qualified implementation referencing a qualified sibling. |
| `compositionBoundary` | A non-composition, non-host project referencing a project whose category is `composition`. |
| `forbiddenPackage` | A package matching a forbidden prefix for the responsibility. |
| `forbiddenFramework` | A `<FrameworkReference>` matching a forbidden prefix for the responsibility. |
| `cycle` | A cycle in the project dependency graph, reported readably. |
| `testReference` | A production project referencing a test project. |
| `waiverIntegrity` | A waiver that is stale, unjustified, malformed, or excuses an unknown rule. Not waivable. |

### Dependency matrix semantics

Both `dependencyMatrix` and `assemblyDependencyMatrix` apply the same three-way rule:

1. **Same responsibility is always allowed.** A qualified implementation referencing its unqualified
   sibling is the same box on the diagram.
2. **`mustNotDependOn` is a violation.**
3. **`mayDependOn` is permitted.**

A reference the matrix neither permits nor forbids is a **violation**: silence on a dependency is not
permission, it is a gap in the policy. Add the responsibility to `mayDependOn` if the dependency is
intended.

### Test and production projects

A project is a **test project** when its name ends with `Tests`, **or** its path contains a `tests`
directory. Test projects are excluded from every rule — a test references everything by design.
Everything else is a **production project**.
