# Type-level rules

A project file only shows a declared `ProjectReference`. It cannot show a type that arrives
transitively through another project, or a type pulled in by a package. The `assemblyDependencyMatrix`
rule closes that gap: it reads the compiled assemblies and applies the **same**
`mayDependOn` / `mustNotDependOn` matrix to the types each one actually references.

It is off by default, because it needs a build to have produced the assemblies:

```json
"assemblyRules": {
  "enabled": true,
  "assemblyRoots": [],
  "assemblyGlobs": ["*.dll"]
}
```

## How it reads

- **Metadata only.** The reader opens each assembly with `System.Reflection.Metadata` and walks the
  `TypeReference` table. Nothing is loaded, executed, or resolved through a probing path, so a type
  initialiser never runs and a missing dependency never throws.
- **The `TypeReference` table is the dependency set.** The compiler emits a row for every external
  type a member signature, base type, interface, generic argument, attribute or method body touches —
  including a type used only inside a method body.
- **References resolve against the scanned set.** A reference is attributed to a project only if the
  defining assembly is itself one of the scanned project assemblies. A package is never mistaken for
  a project: `Microsoft.Extensions.Logging.Abstractions` is not `Abstractions`.

## How an assembly is attributed to a project

1. **The SDK marker.** `Purview.BuildSdk` writes a global `public static partial class
   <ProjectName>Assembly` into every production project, so an assembly whose name is unrelated to
   its project — an explicit `AssemblyName` — still maps exactly. This is the same marker the
   generated assembly identifier exists for.
2. **Exact name.** A repository that opts the marker out, or a project the SDK did not build, maps
   when its assembly name is its project name.

An assembly that maps to no project is a package and is ignored.

## Where the assemblies come from

An empty `assemblyRoots` scans the test output directory (`AppContext.BaseDirectory`), which already
holds every project the test project references, directly or transitively. Set `assemblyRoots` to
repo-relative folders only if the assemblies live somewhere else; `obj` output is always skipped so a
reference assembly never stands in for the real one.

## When it finds nothing

Enabled with no production assemblies found is a **failure**, not a silent pass — the same guard
`graphDiscovery` provides for projects. Build the repository before running the tests, or point
`assemblyRoots` at the right folder.

## Qualified implementations

At the type level, a qualified implementation collapses into its responsibility, exactly as it does
at the project level: a type in `Persistence.SqlServer` referencing a type in `Persistence` is a
same-responsibility reference and is allowed.

## Naming and shape

The same metadata reader also exposes the **declarations** an assembly contains, which powers the
opt-in type rules:

| Rule | Checks |
| -- | -- |
| `typeNaming` | An interface without `I`, an attribute or exception without its suffix, a discouraged suffix |
| `typeShape` | An unsealed public class, a public instance field, a mutable collection return, a non-static utility class |
| `typeNamespace` | A public type outside the project's `RootNamespace` |
| `attributePolicy` | A missing or forbidden attribute, `[Obsolete]` without a message, `InternalsVisibleTo` naming production code |

They share `assemblyRules.assemblyRoots` and `assemblyGlobs`, and are configured under
[`typeRules`](Policy-Reference.md#type-rules). The reader is metadata-only here too: it walks the
definition tables, never loading the assembly. A public type's file path is not in metadata, so
`typeNamespace` compares the declared namespace with `RootNamespace` and makes no claim about
folders.
