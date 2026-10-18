namespace Purview.Architecture.Testing;

/// <summary>The dependency-budget ratchet.</summary>
public sealed partial class ArchitecturePolicyEvaluator
{
	/// <summary>Projects that exceed, or no longer fill, their dependency budget.</summary>
	/// <returns>The violations.</returns>
	/// <remarks>
	/// The rule is deliberately not waivable: the budget is the record that keeps dependency growth
	/// reviewable, so excusing it would make the policy unfalsifiable. Remove the reference, or change
	/// the budget in the same reviewed commit.
	/// </remarks>
	public IReadOnlyList<ArchitectureViolation> DependencyBudgetRule()
	{
		if (!Policy.DependencyBudget.Enabled)
			return [];

		List<ArchitectureViolation> violations = [];
		HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

		foreach (var entry in Policy.DependencyBudget.Entries)
		{
			if (!seen.Add(entry.Project))
			{
				violations.Add(
					new ArchitectureViolation(
						ArchitectureRules.DependencyBudgetRule,
						"(policy)",
						$"The dependency budget declares '{entry.Project}' more than once. Only one entry can apply; merge them."
					)
				);
				continue;
			}

			if (!Projects.TryGetValue(entry.Project, out var project) || project.IsTestProject)
			{
				violations.Add(
					new ArchitectureViolation(
						ArchitectureRules.DependencyBudgetRule,
						"(policy)",
						$"The dependency budget names '{entry.Project}', which is not a production project. Remove the entry."
					)
				);
				continue;
			}

			var allowed = entry.References.ToHashSet(StringComparer.OrdinalIgnoreCase);
			var actual = project.ProjectReferences.ToHashSet(StringComparer.OrdinalIgnoreCase);

			foreach (var added in actual.Except(allowed).Order(StringComparer.OrdinalIgnoreCase))
			{
				violations.Add(
					new ArchitectureViolation(
						ArchitectureRules.DependencyBudgetRule,
						project.Name,
						$"'{project.Name}' references '{added}', which is not in its dependency budget. Remove the "
							+ "reference, or add it to dependencyBudget in a reviewed policy change."
					)
				);
			}

			if (!Policy.DependencyBudget.FailOnStale)
				continue;

			foreach (var stale in allowed.Except(actual).Order(StringComparer.OrdinalIgnoreCase))
			{
				violations.Add(
					new ArchitectureViolation(
						ArchitectureRules.DependencyBudgetRule,
						project.Name,
						$"The dependency budget for '{project.Name}' still allows '{stale}', but the project no longer "
							+ "references it. Tighten the budget: a stale allowance hides the next dependency."
					)
				);
			}
		}

		return violations;
	}

	/// <summary>The adoption rule identifiers enabled by the policy.</summary>
	/// <returns>The identifiers, or an empty list when the budget is off.</returns>
	public IReadOnlyList<string> EnabledAdoptionRules() =>
		Policy.DependencyBudget.Enabled ? [ArchitectureRules.DependencyBudgetRule] : [];

	/// <summary>Evaluates one adoption rule by identifier.</summary>
	/// <param name="rule">The rule identifier.</param>
	/// <returns>The violations.</returns>
	public IReadOnlyList<ArchitectureViolation> EvaluateAdoptionRule(string rule) =>
		rule switch
		{
			ArchitectureRules.DependencyBudgetRule => DependencyBudgetRule(),
			_ => [],
		};
}
