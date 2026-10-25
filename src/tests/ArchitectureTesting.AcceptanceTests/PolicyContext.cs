namespace Purview.Architecture.Testing.AcceptanceTests;

/// <summary>What one behaviour scenario builds up and then asserts against.</summary>
/// <remarks>
/// A scenario describes a repository as a set of declared types, runs one rule over it, and
/// inspects the result. The context carries those three things and nothing else, so a step reads as
/// a sentence about the repository rather than about the evaluator.
/// </remarks>
public class PolicyContext
{
	/// <summary>The project the scenario's types belong to.</summary>
	public const string Project = "Core";

	/// <summary>The project's root namespace, which the namespace rule measures against.</summary>
	public const string RootNamespace = "Purview.Core";

	/// <summary>The type-level settings the rule runs under.</summary>
	public TypeRules TypeRules { get; set; } = new() { Enabled = true };

	/// <summary>The types the repository declares.</summary>
	public List<APIType> Types { get; } = [];

	/// <summary>The violations the rule reported.</summary>
	public IReadOnlyList<ArchitectureViolation> Violations { get; set; } = [];

	/// <summary>The captured API baseline, when the scenario captured one.</summary>
	public APIBaseline? Baseline { get; set; }

	/// <summary>Builds an evaluator over the declared types.</summary>
	/// <returns>The evaluator.</returns>
	public ArchitecturePolicyEvaluator Evaluator()
	{
		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
		{
			[Project] = new(Project, "/repo/src/Core/Core.csproj", [], [], false) { RootNamespace = RootNamespace },
		};

		Dictionary<string, APIAssembly> surfaces = new(StringComparer.OrdinalIgnoreCase)
		{
			[Project] = new(Project, Project, [], [.. Types]),
		};

		return new ArchitecturePolicyEvaluator(
			Policy(),
			projects,
			new Dictionary<string, AssemblyNode>(),
			apiSurfaces: surfaces
		);
	}

	/// <summary>Builds the policy the scenario runs under.</summary>
	/// <returns>The policy.</returns>
	public ArchitecturePolicy Policy() =>
		new()
		{
			Version = 3,
			Responsibilities =
			[
				new Responsibility
				{
					Name = Project,
					Category = "shared",
					Purpose = "The responsibility the acceptance scenarios declare their types in.",
					MayDependOn = [],
					MustNotDependOn = [],
				},
			],
			TypeRules = TypeRules,
		};
}
