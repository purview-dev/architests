namespace Purview.Architecture.Testing;

/// <summary>The policy's own consistency checks.</summary>
public sealed partial class ArchitecturePolicyEvaluator
{
	/// <summary>The policy's own consistency: unknown names, overlapping permissions and duplicates.</summary>
	/// <returns>The violations.</returns>
	/// <remarks>
	/// These are unambiguous policy bugs rather than architectural choices, so the rule always runs.
	/// It is deliberately not waivable: a waiver excusing a broken policy would make the policy
	/// unfalsifiable. Reachability is opt-in because it proves a prohibition cannot be bypassed.
	/// </remarks>
	public IReadOnlyList<ArchitectureViolation> PolicyIntegrity()
	{
		List<ArchitectureViolation> violations = [];

		Dictionary<string, Responsibility> byName = new(StringComparer.OrdinalIgnoreCase);

		foreach (var responsibility in Policy.Responsibilities)
		{
			if (!byName.TryAdd(responsibility.Name, responsibility))
			{
				violations.Add(
					PolicyViolation(
						$"The policy declares the responsibility '{responsibility.Name}' more than once. "
							+ "Only the first declaration classifies a project, so the second is silently ignored."
					)
				);
			}
		}

		violations.AddRange(AliasIntegrity(byName));

		foreach (var responsibility in Policy.Responsibilities)
		{
			violations.AddRange(MatrixIntegrity(responsibility, byName));
		}

		if (Policy.PolicyIntegrity.CheckReachability)
			violations.AddRange(ReachabilityIntegrity(byName));

		return violations;
	}

	/// <summary>Checks that no alias steals a name or means two responsibilities.</summary>
	/// <param name="byName">The responsibilities, keyed by name.</param>
	/// <returns>The violations.</returns>
	List<ArchitectureViolation> AliasIntegrity(Dictionary<string, Responsibility> byName)
	{
		List<ArchitectureViolation> violations = [];
		Dictionary<string, Responsibility> aliases = new(StringComparer.OrdinalIgnoreCase);

		foreach (var responsibility in Policy.Responsibilities)
		{
			foreach (var alias in responsibility.Aliases.Distinct(StringComparer.OrdinalIgnoreCase))
			{
				if (byName.TryGetValue(alias, out var owner))
				{
					violations.Add(
						PolicyViolation(
							$"'{responsibility.Name}' declares the alias '{alias}', which is the responsibility name '{owner.Name}'. "
								+ "A name wins, so the alias can never classify a project."
						)
					);
					continue;
				}

				if (!aliases.TryAdd(alias, responsibility))
				{
					violations.Add(
						PolicyViolation(
							$"The alias '{alias}' is declared by both '{aliases[alias].Name}' and '{responsibility.Name}', "
								+ "so classification would silently pick one."
						)
					);
				}
			}
		}

		return violations;
	}

	/// <summary>Checks one responsibility's dependency lists against the catalogue.</summary>
	/// <param name="responsibility">The responsibility.</param>
	/// <param name="byName">The responsibilities, keyed by name.</param>
	/// <returns>The violations.</returns>
	List<ArchitectureViolation> MatrixIntegrity(
		Responsibility responsibility,
		Dictionary<string, Responsibility> byName
	)
	{
		List<ArchitectureViolation> violations = [];

		foreach (var unknown in responsibility.MayDependOn.Where(target => !byName.ContainsKey(target)))
		{
			violations.Add(UnknownTarget(responsibility, "mayDependOn", unknown, byName));
		}

		foreach (var unknown in responsibility.MustNotDependOn.Where(target => !byName.ContainsKey(target)))
		{
			violations.Add(UnknownTarget(responsibility, "mustNotDependOn", unknown, byName));
		}

		foreach (
			var overlap in responsibility.MayDependOn.Where(target =>
				responsibility.MustNotDependOn.Contains(target, StringComparer.OrdinalIgnoreCase)
			)
		)
		{
			violations.Add(
				PolicyViolation(
					$"'{responsibility.Name}' both permits and forbids '{overlap}'. "
						+ "A dependency cannot be both; remove it from one list."
				)
			);
		}

		if (
			!string.Equals(responsibility.ForbiddenPackageScope, "own", StringComparison.OrdinalIgnoreCase)
			&& !string.Equals(responsibility.ForbiddenPackageScope, "closure", StringComparison.OrdinalIgnoreCase)
		)
		{
			violations.Add(
				PolicyViolation(
					$"'{responsibility.Name}' declares forbiddenPackageScope '{responsibility.ForbiddenPackageScope}', "
						+ "which is not a scope. Use 'own' or 'closure'."
				)
			);
		}

		return violations;
	}

	/// <summary>Builds the violation for a matrix entry naming no responsibility.</summary>
	/// <param name="responsibility">The responsibility that declares the entry.</param>
	/// <param name="list">The list the entry appears in.</param>
	/// <param name="target">The unknown name.</param>
	/// <param name="byName">The responsibilities, keyed by name.</param>
	/// <returns>The violation.</returns>
	static ArchitectureViolation UnknownTarget(
		Responsibility responsibility,
		string list,
		string target,
		Dictionary<string, Responsibility> byName
	) =>
		PolicyViolation(
			$"'{responsibility.Name}' {list} names '{target}', which is not a responsibility. "
				+ $"Known responsibilities: {Names(byName)}. "
				+ "The matrix compares responsibility names, not aliases or project names."
		);

	/// <summary>Proves that no permitted dependency path reaches a forbidden responsibility.</summary>
	/// <param name="byName">The responsibilities, keyed by name.</param>
	/// <returns>The violations.</returns>
	List<ArchitectureViolation> ReachabilityIntegrity(Dictionary<string, Responsibility> byName)
	{
		List<ArchitectureViolation> violations = [];

		foreach (var responsibility in Policy.Responsibilities)
		{
			foreach (var forbidden in responsibility.MustNotDependOn)
			{
				// A direct overlap is already reported; do not report the same bug twice.
				if (responsibility.MayDependOn.Contains(forbidden, StringComparer.OrdinalIgnoreCase))
					continue;

				var path = FindPath(byName, responsibility.Name, forbidden);

				if (path is not null)
				{
					violations.Add(
						PolicyViolation(
							$"'{responsibility.Name}' mustNotDependOn '{forbidden}', but its permitted dependencies reach it: {path}. "
								+ "The prohibition is unenforceable until the edge that closes the path is removed."
						)
					);
				}
			}
		}

		return violations;
	}

	/// <summary>Builds a violation that belongs to the policy rather than to a project.</summary>
	/// <param name="message">What is wrong and how to fix it.</param>
	/// <returns>The violation.</returns>
	static ArchitectureViolation PolicyViolation(string message) =>
		new(ArchitectureRules.PolicyIntegrity, "(policy)", message);

	/// <summary>Renders the known responsibility names for a failure message.</summary>
	/// <param name="byName">The responsibilities, keyed by name.</param>
	/// <returns>The names.</returns>
	static string Names(Dictionary<string, Responsibility> byName) =>
		byName.Count == 0 ? "(none)" : string.Join(", ", byName.Keys);

	/// <summary>Finds the shortest permitted dependency path between two responsibilities.</summary>
	/// <param name="byName">The responsibilities, keyed by name.</param>
	/// <param name="start">The responsibility whose permissions are followed.</param>
	/// <param name="target">The responsibility the path must reach.</param>
	/// <returns>The path, or <see langword="null"/> when it cannot be reached.</returns>
	static string? FindPath(Dictionary<string, Responsibility> byName, string start, string target)
	{
		Queue<IReadOnlyList<string>> queue = new();
		HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase) { start };
		queue.Enqueue([start]);

		while (queue.Count > 0)
		{
			var path = queue.Dequeue();

			if (!byName.TryGetValue(path[^1], out var current))
				continue;

			foreach (var next in current.MayDependOn)
			{
				// An unknown name is reported on its own; following it would add noise.
				if (!byName.ContainsKey(next))
					continue;

				if (string.Equals(next, target, StringComparison.OrdinalIgnoreCase))
					return string.Join(" -> ", path.Append(next));

				if (!visited.Add(next))
					continue;

				queue.Enqueue([.. path, next]);
			}
		}

		return null;
	}
}
