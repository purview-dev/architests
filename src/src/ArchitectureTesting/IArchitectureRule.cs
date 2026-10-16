namespace Purview.Architecture.Testing;

/// <summary>Everything a rule is evaluated against.</summary>
/// <param name="Policy">The policy being applied.</param>
/// <param name="Projects">The project graph, keyed by project name.</param>
/// <param name="Assemblies">The compiled assemblies, keyed by project name. Empty when type-level rules are off.</param>
public sealed record ArchitectureContext(
	ArchitecturePolicy Policy,
	IReadOnlyDictionary<string, ProjectNode> Projects,
	IReadOnlyDictionary<string, AssemblyNode> Assemblies
);

/// <summary>
/// A rule a repository adds to the standard set.
/// </summary>
/// <remarks>
/// This is the supported extension point: an additional rule returns the same
/// <see cref="ArchitectureViolation"/> type as the built-in rules, so it participates in the same
/// failure reporting, <c>SkipRule</c> opt-out and waiver model. A repository expresses a bespoke
/// check without a second architecture framework.
/// </remarks>
public interface IArchitectureRule
{
	/// <summary>The rule identifier, used for the test name, <c>SkipRule</c> and waivers.</summary>
	string Rule { get; }

	/// <summary>Evaluates the rule.</summary>
	/// <param name="context">The policy, projects and assemblies.</param>
	/// <returns>The violations, empty when the rule is satisfied.</returns>
	IReadOnlyList<ArchitectureViolation> Evaluate(ArchitectureContext context);
}
