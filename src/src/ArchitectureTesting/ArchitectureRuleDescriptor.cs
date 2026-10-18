namespace Purview.Architecture.Testing;

/// <summary>Describes one rule in the standard set.</summary>
/// <param name="Rule">The rule identifier, used by the test name, <c>SkipRule</c> and waivers.</param>
/// <param name="Title">A short human-readable title.</param>
/// <param name="Summary">What the rule catches, in one sentence.</param>
/// <param name="DataSource">What the rule reads: the policy, the project graph or the assemblies.</param>
/// <param name="Waivable">Whether a policy waiver may excuse the rule.</param>
/// <param name="Group">
/// The opt-in policy group that enables the rule, or empty when the rule is always on.
/// </param>
/// <param name="DocumentationAnchor">The documentation heading a failure can point at.</param>
/// <remarks>
/// The catalogue exists so the rule set has one authority. <see cref="ArchitectureRules.All"/> is
/// derived from it, the documentation test asserts every identifier appears in the docs and skills,
/// and <see cref="ArchitectureRules.IsWaivable"/> answers from it rather than from a second list.
/// </remarks>
public sealed record ArchitectureRuleDescriptor(
	string Rule,
	string Title,
	string Summary,
	string DataSource,
	bool Waivable,
	string Group = "",
	string DocumentationAnchor = ""
);
