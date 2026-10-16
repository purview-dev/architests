# Policy reference

The policy is `.config/architecture-policy.json`. It is validated against the schema embedded in the
package before any rule runs, so a field the reader understands but the schema rejects is a bug.

The authoritative definition is
[`architecture-policy.schema.json`](https://github.com/purview-dev/architests/blob/main/architecture-policy.schema.json).

## Top-level fields

| Field | Type | Default | Meaning |
| -- | -- | -- | -- |
| `$schema` | string | — | Pointer to the published schema, so an editor validates as you type. |
| `version` | integer ≥ 1 | — | The document version. The current version is **2**. |
| `description` | string | — | Free text. |
| `conventions` | object | — | `qualifiedNameSeparator` and `dependencyArrowMeaning`, for humans and editors. |
| `sourceRoots` | string[] | `["src"]` | Repo-relative folders holding the projects. An empty array scans the repository root. |
| `responsibilities` | object[] | — | **Required**, at least one. The responsibility catalogue. |
| `discouragedProjectNames` | object | — | Names that describe a category, with a `reason`. |
| `assemblyRules` | object | — | The type-level check. Off unless `enabled` is true. |
| `waivers` | object[] | `[]` | Deliberate, reviewed departures. |

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

## Waivers

| Field | Type | Required | Meaning |
| -- | -- | -- | -- |
| `project` | string | ✅ | The project the waiver applies to, exactly as it appears on disk. |
| `mapsTo` | string | | The responsibility the project actually has. Keeps every dependency rule in force while excusing the name. |
| `rules` | string[] | ✅ | The rule ids this waiver excuses. At least one. |
| `reason` | string ≥ 40 | ✅ | Why the departure is acceptable. |
| `reviewedOn` | date | | When the departure was last reviewed. |

Any rule id may be waived, including `siblingIndependence` and `assemblyDependencyMatrix`. An unknown
id is a `waiverIntegrity` failure, so a typo cannot silently excuse nothing.

## Rules

| Rule id | Catches |
| -- | -- |
| `graphDiscovery` | No production projects were discovered, so every other rule would pass vacuously. Not waivable. |
| `naming` | A production project whose name maps to no responsibility or alias. |
| `discouragedName` | A project named after a category, so nothing can be kept out of it. |
| `dependencyMatrix` | A declared `ProjectReference` the matrix forbids or does not permit. |
| `assemblyDependencyMatrix` | A type reference the matrix forbids or does not permit, including one that arrives transitively or through a package. |
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
