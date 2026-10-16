# Extending

A repository can add a bespoke check without a second architecture framework. Implement
`IArchitectureRule` and return the same `ArchitectureViolation` type as the built-in rules:

```csharp
using Purview.Architecture.Testing;

[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests
{
    protected override IReadOnlyList<IArchitectureRule> AdditionalRules => [new NoConsoleLoggingRule()];

    sealed class NoConsoleLoggingRule : IArchitectureRule
    {
        public string Rule => "noConsoleLogging";

        public IReadOnlyList<ArchitectureViolation> Evaluate(ArchitectureContext context) =>
            context.Projects.Values
                .Where(project => !project.IsTestProject)
                // ...inspect the project or its assembly...
                .Select(project => new ArchitectureViolation(Rule, project.Name, "Console logging is banned here."))
                .ToArray();
    }
}
```

## What the context gives you

`ArchitectureContext` carries the three inputs a rule can need:

| Member | What it is |
| -- | -- |
| `Policy` | The parsed `ArchitecturePolicy`, so a rule reads the same definition the built-in rules do. |
| `Projects` | The project graph, keyed by project name, with declared references, packages, framework references and analyzers. |
| `Assemblies` | The compiled assemblies, keyed by project name, each with the external types it references. Empty when `assemblyRules.enabled` is false. |

## What you get for free

An additional rule participates in everything the built-in rules do:

- **Its own `[Test]`.** Each rule id becomes a case of
  `AdditionalRule_GivenTheRepository_RespectsThePolicy`, so a failure names the rule.
- **`SkipRule`.** Override `SkipRule` and return `true` for the rule's `Rule` id.
- **Waivers.** A waiver whose `rules` names the rule's id is honoured automatically, so a reviewed,
  per-project departure needs no code.
- **The same reporting.** `ArchitecturePolicyEvaluator.Report` renders the violations, and the
  failure's `.Because(...)` lists every one.

## Keep the rule in a fixture

The evaluator takes an in-memory graph, so a rule is testable as a small fixture rather than a
repository on disk:

```csharp
var evaluator = new ArchitecturePolicyEvaluator(
    policy,
    projects,
    assemblies,
    [new NoConsoleLoggingRule()]
);

await Assert.That(evaluator.EvaluateAdditionalRule("noConsoleLogging")).IsEmpty();
```

That is exactly how the package's own rules are tested.
