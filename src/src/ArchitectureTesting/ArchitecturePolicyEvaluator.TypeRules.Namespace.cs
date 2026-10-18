namespace Purview.Architecture.Testing;

/// <summary>The namespace rule and the type-rule dispatch.</summary>
public sealed partial class ArchitecturePolicyEvaluator
{
	/// <summary>Types declared outside their project's root namespace.</summary>
	/// <returns>The violations.</returns>
	/// <remarks>
	/// Metadata records the namespace, not the folder, so this checks the declared root and makes no
	/// claim about where the file sits.
	/// </remarks>
	public IReadOnlyList<ArchitectureViolation> TypeNamespace()
	{
		List<ArchitectureViolation> violations = [];

		foreach (var (project, type) in GovernedTypes())
		{
			if (!Projects.TryGetValue(project, out var node))
				continue;

			var root = node.RootNamespace;

			if (string.IsNullOrWhiteSpace(root))
				continue;

			if (
				string.Equals(type.Namespace, root, StringComparison.Ordinal)
				|| type.Namespace.StartsWith(root + ".", StringComparison.Ordinal)
			)
			{
				continue;
			}

			violations.Add(
				TypeRuleViolation(
					ArchitectureRules.TypeNamespace,
					project,
					$"'{type.FullName}' is declared in namespace '{type.Namespace}', which is outside the project's "
						+ $"root namespace '{root}'. Set RootNamespace on '{project}', or move the type under it."
				)
			);
		}

		return Excuse(violations);
	}

	/// <summary>The type-level rule identifiers enabled by the policy.</summary>
	/// <returns>
	/// The identifiers, or an empty list when type rules are off, so a disabled group produces no
	/// test rather than a test that checks nothing.
	/// </returns>
	public IReadOnlyList<string> EnabledTypeRules() =>
		Policy.TypeRules.Enabled
			?
			[
				ArchitectureRules.TypeNaming,
				ArchitectureRules.TypeShape,
				ArchitectureRules.TypeNamespace,
				ArchitectureRules.AttributePolicy,
			]
			: [];

	/// <summary>Evaluates one type-level rule by identifier.</summary>
	/// <param name="rule">The rule identifier.</param>
	/// <returns>The violations, with waived rules excused.</returns>
	public IReadOnlyList<ArchitectureViolation> EvaluateTypeRule(string rule) =>
		rule switch
		{
			ArchitectureRules.TypeNaming => TypeNaming(),
			ArchitectureRules.TypeShape => TypeShape(),
			ArchitectureRules.TypeNamespace => TypeNamespace(),
			ArchitectureRules.AttributePolicy => AttributePolicy(),
			_ => [],
		};
}
