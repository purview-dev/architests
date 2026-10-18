# Purview Architecture Testing

Enforce a repository's architecture policy as TUnit tests, so the rules are defined once rather than
copied into every repository's test project.

Adoption is a policy file at `.config/architecture-policy.json` and one derived test class:

```csharp
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests;
```

[Get started](Getting-Started.md){ .md-button .md-button--primary }

## Guides

- [Getting started](Getting-Started.md) — adopt the package in a repository, step by step.
- [Policy reference](Policy-Reference.md) — every field, every rule id, every default.
- [Type-level rules](Type-Level-Rules.md) — how the assembly check works and how an assembly is
  attributed to a project.
- [API contracts](Contracts.md) — the committed baseline, XML documentation and the port rules.
- [Migration and budget](Migration.md) — adopt incrementally, waive narrowly and ratchet the graph.
- [Command line](Cli.md) — validate, capture the API, review waivers and render the graph.
- [Extending](Extending.md) — add your own rule to the standard set.

## Reference

- [The policy schema](https://github.com/purview-dev/architests/blob/main/architecture-policy.schema.json)
- [The sample](https://github.com/purview-dev/architests/tree/main/src/samples/ArchitectureSample)
- [The package README](https://github.com/purview-dev/architests/blob/main/src/src/ArchitectureTesting/README.md)

## Agent skills

```bash
npx skills add purview-dev/architests
```
