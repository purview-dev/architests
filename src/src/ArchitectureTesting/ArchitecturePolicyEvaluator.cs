namespace Purview.Architecture.Testing;

/// <summary>
/// Evaluates a repository's project and assembly graph against an architecture policy.
/// </summary>
/// <remarks>
/// Deliberately free of MSBuild and test-framework types: the rules are the part worth testing
/// directly, and coupling them to a test base class would make them reachable only through one.
/// <see cref="PurviewArchitectureTests"/> is a thin wrapper over this.
/// </remarks>
public sealed partial class ArchitecturePolicyEvaluator
{
	readonly IReadOnlyList<IArchitectureRule> _additionalRules;
	readonly DateOnly _today;

	/// <summary>Creates an evaluator over an explicit policy and project graph.</summary>
	/// <param name="policy">The policy to apply.</param>
	/// <param name="projects">The projects to evaluate, keyed by name.</param>
	public ArchitecturePolicyEvaluator(ArchitecturePolicy policy, IReadOnlyDictionary<string, ProjectNode> projects)
		: this(policy, projects, new Dictionary<string, AssemblyNode>(), []) { }

	/// <summary>Creates an evaluator over an explicit policy, project graph and assembly graph.</summary>
	/// <param name="policy">The policy to apply.</param>
	/// <param name="projects">The projects to evaluate, keyed by name.</param>
	/// <param name="assemblies">The compiled assemblies to evaluate, keyed by project name.</param>
	/// <param name="additionalRules">Rules a repository adds to the standard set.</param>
	/// <param name="today">
	/// The date waiver expiry is measured from. Defaults to today in UTC, so a test can pin the clock.
	/// </param>
	/// <param name="apiSurfaces">The declared types of each compiled assembly, keyed by project name.</param>
	/// <param name="apiBaseline">The committed API baseline, when one was found.</param>
	public ArchitecturePolicyEvaluator(
		ArchitecturePolicy policy,
		IReadOnlyDictionary<string, ProjectNode> projects,
		IReadOnlyDictionary<string, AssemblyNode> assemblies,
		IReadOnlyList<IArchitectureRule>? additionalRules = null,
		DateOnly? today = null,
		IReadOnlyDictionary<string, APIAssembly>? apiSurfaces = null,
		APIBaseline? apiBaseline = null
	)
	{
		ArgumentNullException.ThrowIfNull(policy);
		ArgumentNullException.ThrowIfNull(projects);
		ArgumentNullException.ThrowIfNull(assemblies);

		Policy = policy;
		Projects = projects;
		Assemblies = assemblies;
		APISurfaces = apiSurfaces ?? new Dictionary<string, APIAssembly>();
		Baseline = apiBaseline;
		_additionalRules = additionalRules ?? [];
		_today = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
	}

	/// <summary>Creates an evaluator for a repository on disk.</summary>
	/// <param name="repositoryRoot">The repository root, or <see langword="null"/> to probe upward for one.</param>
	/// <param name="additionalRules">Rules a repository adds to the standard set.</param>
	/// <param name="today">The date waiver expiry is measured from, or <see langword="null"/> for today.</param>
	/// <returns>The evaluator.</returns>
	public static ArchitecturePolicyEvaluator ForRepository(
		string? repositoryRoot = null,
		IReadOnlyList<IArchitectureRule>? additionalRules = null,
		DateOnly? today = null
	)
	{
		var root = repositoryRoot ?? ArchitecturePolicy.RepositoryRoot();

		var policy = ArchitecturePolicy.Load(root);

		var projects = ProjectGraph.Load(root, policy.SourceRoots);

		// Every reader scans the same assemblies, so the glob is walked once.
		var needsAssemblies = policy.AssemblyRules.Enabled;
		var needsSurfaces = policy.TypeRules.Enabled || policy.APIRules.Enabled || policy.PortRules.Enabled;
		var assemblyPaths = needsAssemblies || needsSurfaces ? AssemblyGraph.Discover(root, policy.AssemblyRules) : [];

		var assemblies = needsAssemblies
			? AssemblyGraph.Load(assemblyPaths, projects)
			: new Dictionary<string, AssemblyNode>();

		var apiSurfaces = needsSurfaces
			? APISurface.Load(assemblyPaths, projects)
			: new Dictionary<string, APIAssembly>();

		var apiBaseline = policy.APIRules.Enabled ? ReadBaseline(root, policy) : null;

		return new ArchitecturePolicyEvaluator(
			policy,
			projects,
			assemblies,
			additionalRules,
			today,
			apiSurfaces,
			apiBaseline
		);
	}

	/// <summary>The policy being applied.</summary>
	public ArchitecturePolicy Policy { get; }

	/// <summary>The project graph.</summary>
	public IReadOnlyDictionary<string, ProjectNode> Projects { get; }

	/// <summary>The compiled assemblies, keyed by project name.</summary>
	public IReadOnlyDictionary<string, AssemblyNode> Assemblies { get; }

	/// <summary>The declared types of each compiled assembly, keyed by project name.</summary>
	public IReadOnlyDictionary<string, APIAssembly> APISurfaces { get; }

	/// <summary>The committed API baseline, or <see langword="null"/> when it is absent or unreadable.</summary>
	public APIBaseline? Baseline { get; }

	/// <summary>The production projects, excluding test projects.</summary>
	public IEnumerable<ProjectNode> ProductionProjects => Projects.Values.Where(project => !project.IsTestProject);

	/// <summary>Runs every rule.</summary>
	/// <returns>Every violation found, in rule order.</returns>
	public IReadOnlyList<ArchitectureViolation> EvaluateAll() =>
		[
			.. PolicyIntegrity(),
			.. GraphDiscovery(),
			.. Naming(),
			.. DiscouragedNames(),
			.. DependencyMatrix(),
			.. AssemblyDependencyMatrix(),
			.. TypeNaming(),
			.. TypeShape(),
			.. TypeNamespace(),
			.. AttributePolicy(),
			.. APISurfaceCompatibility(),
			.. APIDocumentation(),
			.. PortImplementation(),
			.. PortPurity(),
			.. SiblingIndependence(),
			.. CompositionBoundary(),
			.. ForbiddenPackages(),
			.. ForbiddenFrameworkReferences(),
			.. Cycles(),
			.. TestReferences(),
			.. DependencyBudgetRule(),
			.. WaiverIntegrity(),
			.. EvaluateAdditionalRules(),
		];

	/// <summary>Reports when no projects were discovered at all.</summary>
	/// <returns>The violations.</returns>
	/// <remarks>
	/// Every other rule is satisfied by an empty graph, so without this a repository whose layout the
	/// scan does not match gets a completely green architecture suite that checked nothing. Emptiness
	/// has to be the loud failure, not the quiet success. It is deliberately not waivable: it is the
	/// guard that stops every other rule passing vacuously.
	/// </remarks>
	public IReadOnlyList<ArchitectureViolation> GraphDiscovery() =>
		ProductionProjects.Any()
			? []
			:
			[
				new ArchitectureViolation(
					ArchitectureRules.GraphDiscovery,
					"(repository)",
					"No production projects were discovered, so every other rule would pass without checking anything. "
						+ $"Looked under: {(Policy.SourceRoots.Count == 0 ? "(repository root)" : string.Join(", ", Policy.SourceRoots))}. "
						+ "Set 'sourceRoots' in the policy to the folders holding the projects; an empty array scans the repository root."
				),
			];

	/// <summary>Projects whose names map to no responsibility.</summary>
	/// <returns>The violations.</returns>
	public IReadOnlyList<ArchitectureViolation> Naming() =>
		Excuse(
			ProductionProjects
				.Where(project => Policy.Classify(project.Name) is null)
				.Select(project => new ArchitectureViolation(
					ArchitectureRules.Naming,
					project.Name,
					$"'{project.Name}' does not map to a known responsibility or alias. "
						+ "Rename it to '<Responsibility>[.<Implementation>]', or record a waiver in the policy. "
						+ $"Known responsibilities: {string.Join(", ", Policy.Responsibilities.Select(responsibility => responsibility.Name))}."
				))
		);

	/// <summary>Projects named after a category rather than a responsibility.</summary>
	/// <returns>The violations.</returns>
	public IReadOnlyList<ArchitectureViolation> DiscouragedNames() =>
		Excuse(
			ProductionProjects
				.Where(project =>
					Policy.DiscouragedProjectNames.Names.Contains(
						project.Name.Split('.')[0],
						StringComparer.OrdinalIgnoreCase
					)
				)
				.Select(project => new ArchitectureViolation(
					ArchitectureRules.DiscouragedName,
					project.Name,
					$"'{project.Name}': {Policy.DiscouragedProjectNames.Reason}"
				))
		);

	/// <summary>References the dependency matrix forbids or does not permit.</summary>
	/// <returns>The violations.</returns>
	public IReadOnlyList<ArchitectureViolation> DependencyMatrix()
	{
		List<ArchitectureViolation> violations = [];

		foreach (var project in ProductionProjects)
		{
			var responsibility = Policy.Classify(project.Name);
			if (responsibility is null)
				continue;

			foreach (var reference in project.ProjectReferences)
			{
				var referenced = Policy.Classify(reference);
				if (referenced is null)
					continue;

				// A qualified sibling is the same box on the diagram, so sibling independence governs
				// it rather than the matrix.
				if (string.Equals(referenced.Name, responsibility.Name, StringComparison.OrdinalIgnoreCase))
					continue;

				if (responsibility.MustNotDependOn.Contains(referenced.Name, StringComparer.OrdinalIgnoreCase))
				{
					violations.Add(
						new ArchitectureViolation(
							ArchitectureRules.DependencyMatrix,
							project.Name,
							$"'{project.Name}' ({responsibility.Name}) references '{reference}' ({referenced.Name}), "
								+ $"but {responsibility.Name} must not depend on {referenced.Name}. "
								+ $"Invert it: {referenced.Name} may depend on {responsibility.Name}, or introduce an abstraction "
								+ $"that {responsibility.Name} owns and {referenced.Name} implements."
						)
					);
				}
				else if (!responsibility.MayDependOn.Contains(referenced.Name, StringComparer.OrdinalIgnoreCase))
				{
					violations.Add(
						new ArchitectureViolation(
							ArchitectureRules.DependencyMatrix,
							project.Name,
							$"'{project.Name}' ({responsibility.Name}) references '{reference}' ({referenced.Name}), "
								+ "which the policy neither permits nor forbids. "
								+ $"{responsibility.Name} may depend on: {string.Join(", ", responsibility.MayDependOn)}. "
								+ $"Add {referenced.Name} to mayDependOn in the policy if this is intended."
						)
					);
				}
			}
		}

		return Excuse(violations);
	}

	/// <summary>Type-level references the dependency matrix forbids or does not permit.</summary>
	/// <returns>The violations.</returns>
	/// <remarks>
	/// This catches what a project file cannot: a type in one responsibility using a type in another,
	/// including a dependency that only arrives transitively through a project or a package. It
	/// mirrors <see cref="DependencyMatrix"/> exactly, including the same-responsibility exemption, so
	/// the policy is declared once.
	/// </remarks>
	public IReadOnlyList<ArchitectureViolation> AssemblyDependencyMatrix()
	{
		if (!Policy.AssemblyRules.Enabled)
			return [];

		var sources = Assemblies
			.Values.Where(assembly =>
				Projects.TryGetValue(assembly.ProjectName, out var project) && !project.IsTestProject
			)
			.ToArray();

		if (sources.Length == 0)
		{
			return
			[
				new ArchitectureViolation(
					ArchitectureRules.AssemblyDependencyMatrix,
					"(repository)",
					"No compiled assemblies were found for the repository's production projects, so the type-level "
						+ "dependency rule would pass without checking anything. Build the repository before running the tests, "
						+ "or set 'assemblyRules.assemblyRoots' in the policy to the folders holding the assemblies."
				),
			];
		}

		List<ArchitectureViolation> violations = [];

		foreach (var assembly in sources)
		{
			var responsibility = Policy.Classify(assembly.ProjectName);
			if (responsibility is null)
				continue;

			var targets = assembly
				.References.Where(reference => reference.TargetProject.Length > 0)
				.GroupBy(reference => reference.TargetProject, StringComparer.OrdinalIgnoreCase);

			foreach (var target in targets)
			{
				var referenced = Policy.Classify(target.Key);
				if (referenced is null)
					continue;

				// A qualified sibling is the same box on the diagram.
				if (string.Equals(referenced.Name, responsibility.Name, StringComparison.OrdinalIgnoreCase))
					continue;

				var first = target.First();
				var additional = target.Count() > 1 ? $" (and {target.Count() - 1} more type(s))" : string.Empty;

				if (responsibility.MustNotDependOn.Contains(referenced.Name, StringComparer.OrdinalIgnoreCase))
				{
					violations.Add(
						new ArchitectureViolation(
							ArchitectureRules.AssemblyDependencyMatrix,
							assembly.ProjectName,
							$"'{assembly.ProjectName}' ({responsibility.Name}) uses '{first.TypeName}' from '{target.Key}' "
								+ $"({referenced.Name}){additional}, but {responsibility.Name} must not depend on {referenced.Name} "
								+ $"at the type level. Invert it: {referenced.Name} may depend on {responsibility.Name}, or introduce "
								+ $"an abstraction that {responsibility.Name} owns and {referenced.Name} implements."
						)
					);
				}
				else if (!responsibility.MayDependOn.Contains(referenced.Name, StringComparer.OrdinalIgnoreCase))
				{
					violations.Add(
						new ArchitectureViolation(
							ArchitectureRules.AssemblyDependencyMatrix,
							assembly.ProjectName,
							$"'{assembly.ProjectName}' ({responsibility.Name}) uses '{first.TypeName}' from '{target.Key}' "
								+ $"({referenced.Name}){additional}, which the policy neither permits nor forbids. "
								+ $"{responsibility.Name} may depend on: {string.Join(", ", responsibility.MayDependOn)}. "
								+ $"Add {referenced.Name} to mayDependOn in the policy if this is intended."
						)
					);
				}
			}
		}

		return Excuse(violations);
	}

	/// <summary>Qualified implementations referencing a qualified sibling.</summary>
	/// <returns>The violations.</returns>
	public IReadOnlyList<ArchitectureViolation> SiblingIndependence()
	{
		List<ArchitectureViolation> violations = [];

		foreach (var project in ProductionProjects)
		{
			var responsibility = Policy.Classify(project.Name);
			if (responsibility?.SiblingIndependence != true || !project.Name.Contains('.', StringComparison.Ordinal))
				continue;

			foreach (var reference in project.ProjectReferences)
			{
				// Only a qualified sibling is a peer; the unqualified project is shared ground.
				var isQualifiedSibling =
					reference.Contains('.', StringComparison.Ordinal)
					&& !string.Equals(reference, project.Name, StringComparison.OrdinalIgnoreCase)
					&& string.Equals(
						Policy.Classify(reference)?.Name,
						responsibility.Name,
						StringComparison.OrdinalIgnoreCase
					);

				if (isQualifiedSibling)
				{
					violations.Add(
						new ArchitectureViolation(
							ArchitectureRules.SiblingIndependence,
							project.Name,
							$"'{project.Name}' references its sibling '{reference}'. Sibling {responsibility.Name} implementations "
								+ $"must stay independent; move the shared part into the unqualified '{responsibility.Name}' project "
								+ "or into an abstraction both can depend on."
						)
					);
				}
			}
		}

		return Excuse(violations);
	}

	/// <summary>Non-host projects referencing the composition root.</summary>
	/// <returns>The violations.</returns>
	/// <remarks>
	/// The composition root is identified by <c>category == "composition"</c> rather than by a literal
	/// name, so a repository that calls it something else still gets the rule.
	/// </remarks>
	public IReadOnlyList<ArchitectureViolation> CompositionBoundary() =>
		Excuse(
			ProductionProjects
				.Where(project =>
					!IsCategory(project.Name, "composition")
					&& !IsCategory(project.Name, "host")
					&& project.ProjectReferences.Any(reference => IsCategory(reference, "composition"))
				)
				.Select(project => new ArchitectureViolation(
					ArchitectureRules.CompositionBoundary,
					project.Name,
					$"'{project.Name}' references the composition root. Composition selects implementations, so it depends on them - "
						+ "never the other way round, which would make the choice circular."
				))
		);

	/// <summary>Packages matching a forbidden prefix for their responsibility.</summary>
	/// <returns>The violations.</returns>
	public IReadOnlyList<ArchitectureViolation> ForbiddenPackages()
	{
		List<ArchitectureViolation> violations = [];

		foreach (var project in ProductionProjects)
		{
			var responsibility = Policy.Classify(project.Name);
			if (responsibility is null)
				continue;

			// The declared reference is what a reviewer adds; the resolved closure is what restore
			// actually brought in, including a transitive or centrally injected package.
			foreach (
				var package in project
					.PackageReferences.Concat(project.ResolvedPackages)
					.Distinct(StringComparer.OrdinalIgnoreCase)
			)
			{
				var forbidden = responsibility.ForbiddenPackagePrefixes.FirstOrDefault(prefix =>
					package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
				);

				if (forbidden is not null)
				{
					violations.Add(
						new ArchitectureViolation(
							ArchitectureRules.ForbiddenPackage,
							project.Name,
							$"'{project.Name}' ({responsibility.Name}) references package '{package}' (declared or resolved by restore, including transitively), which matches the forbidden "
								+ $"prefix '{forbidden}' for {responsibility.Name}. {responsibility.Purpose} "
								+ "Move the dependency to the responsibility that owns that technology."
						)
					);
				}
			}
		}

		return Excuse(violations);
	}

	/// <summary>Framework references matching a forbidden prefix for their responsibility.</summary>
	/// <returns>The violations.</returns>
	public IReadOnlyList<ArchitectureViolation> ForbiddenFrameworkReferences()
	{
		List<ArchitectureViolation> violations = [];

		foreach (var project in ProductionProjects)
		{
			var responsibility = Policy.Classify(project.Name);
			if (responsibility is null)
				continue;

			foreach (var framework in project.FrameworkReferences)
			{
				var forbidden = responsibility.ForbiddenFrameworkReferences.FirstOrDefault(prefix =>
					framework.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
				);

				if (forbidden is not null)
				{
					violations.Add(
						new ArchitectureViolation(
							ArchitectureRules.ForbiddenFramework,
							project.Name,
							$"'{project.Name}' ({responsibility.Name}) references framework '{framework}', which matches the forbidden "
								+ $"prefix '{forbidden}' for {responsibility.Name}. {responsibility.Purpose} "
								+ "Move the framework reference to the responsibility that owns that technology."
						)
					);
				}
			}
		}

		return Excuse(violations);
	}

	/// <summary>Cycles in the project dependency graph.</summary>
	/// <returns>The violations.</returns>
	public IReadOnlyList<ArchitectureViolation> Cycles() =>
		Excuse(
			ProjectGraph
				.FindCycles(Projects)
				.Select(cycle => new ArchitectureViolation(
					ArchitectureRules.Cycle,
					cycle.Split(" -> ")[0],
					$"Dependency cycle: {cycle}. Break it by moving the shared part into a project both can depend on."
				))
		);

	/// <summary>Production projects referencing a test project.</summary>
	/// <returns>The violations.</returns>
	public IReadOnlyList<ArchitectureViolation> TestReferences() =>
		Excuse(
			ProductionProjects.SelectMany(project =>
				project
					.ProjectReferences.Where(reference =>
						Projects.TryGetValue(reference, out var target) && target.IsTestProject
					)
					.Select(reference => new ArchitectureViolation(
						ArchitectureRules.TestReference,
						project.Name,
						$"'{project.Name}' references the test project '{reference}'. Test helpers must not ship."
					))
			)
		);

	/// <summary>Waivers that are stale, unjustified or malformed.</summary>
	/// <returns>The violations.</returns>
	/// <remarks>
	/// A waiver nobody revisits is how a policy quietly stops being true, so the waivers are
	/// themselves subject to rules. It is deliberately not waivable: a waiver excusing a broken
	/// waiver would make the policy unfalsifiable.
	/// </remarks>
	public IReadOnlyList<ArchitectureViolation> WaiverIntegrity()
	{
		List<ArchitectureViolation> violations = [];

		var knownRules = ArchitectureRules
			.All.Concat(_additionalRules.Select(rule => rule.Rule))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		foreach (var waiver in Policy.Waivers)
		{
			if (!Projects.ContainsKey(waiver.Project))
			{
				violations.Add(
					new ArchitectureViolation(
						ArchitectureRules.WaiverIntegrity,
						waiver.Project,
						$"The waiver for '{waiver.Project}' names a project that does not exist. Remove it - a stale waiver hides the next one."
					)
				);
			}

			if (waiver.Rules.Count == 0)
			{
				violations.Add(
					new ArchitectureViolation(
						ArchitectureRules.WaiverIntegrity,
						waiver.Project,
						$"The waiver for '{waiver.Project}' excuses no named rule, so it does nothing."
					)
				);
			}

			foreach (var unknown in waiver.Rules.Where(rule => !knownRules.Contains(rule)))
			{
				violations.Add(
					new ArchitectureViolation(
						ArchitectureRules.WaiverIntegrity,
						waiver.Project,
						$"The waiver for '{waiver.Project}' excuses unknown rule '{unknown}', so it does nothing. "
							+ $"Known rules: {string.Join(", ", knownRules)}."
					)
				);
			}

			foreach (
				var unwaivable in waiver.Rules.Where(rule =>
					knownRules.Contains(rule) && !ArchitectureRules.IsWaivable(rule)
				)
			)
			{
				violations.Add(
					new ArchitectureViolation(
						ArchitectureRules.WaiverIntegrity,
						waiver.Project,
						$"The waiver for '{waiver.Project}' excuses '{unwaivable}', which cannot be waived: it guards the policy itself. "
							+ "Remove it from the waiver."
					)
				);
			}

			if (waiver.MapsTo.Length > 0 && Policy.Classify(waiver.Project) is null)
			{
				violations.Add(
					new ArchitectureViolation(
						ArchitectureRules.WaiverIntegrity,
						waiver.Project,
						$"The waiver for '{waiver.Project}' maps to unknown responsibility '{waiver.MapsTo}', so no dependency rule applies to it."
					)
				);
			}
		}

		// A waiver nobody revisits is how a policy quietly stops being true.
		foreach (var review in WaiverReviewer.Review(Policy, _today).Where(review => review.IsStale))
		{
			violations.Add(
				new ArchitectureViolation(
					ArchitectureRules.WaiverIntegrity,
					review.Waiver.Project,
					$"The waiver for '{review.Waiver.Project}' {review.Reason}"
				)
			);
		}

		return violations;
	}

	/// <summary>Evaluates the rules a repository added to the standard set.</summary>
	/// <returns>The violations, with waived rules excused.</returns>
	public IReadOnlyList<ArchitectureViolation> EvaluateAdditionalRules() =>
		[
			.. _additionalRules.SelectMany(rule =>
				Excuse(rule.Evaluate(new ArchitectureContext(Policy, Projects, Assemblies, APISurfaces, Baseline)))
			),
		];

	/// <summary>Evaluates one additional rule by identifier.</summary>
	/// <param name="rule">The rule identifier.</param>
	/// <returns>The violations, with waived rules excused.</returns>
	public IReadOnlyList<ArchitectureViolation> EvaluateAdditionalRule(string rule) =>
		[
			.. _additionalRules
				.Where(candidate => string.Equals(candidate.Rule, rule, StringComparison.OrdinalIgnoreCase))
				.SelectMany(candidate =>
					Excuse(
						candidate.Evaluate(new ArchitectureContext(Policy, Projects, Assemblies, APISurfaces, Baseline))
					)
				),
		];

	/// <summary>Renders violations as a numbered list for a failure message.</summary>
	/// <param name="violations">The violations to render.</param>
	/// <returns>The rendered report.</returns>
	public static string Report(IReadOnlyList<ArchitectureViolation> violations)
	{
		ArgumentNullException.ThrowIfNull(violations);

		return violations.Count == 0
			? "no violations"
			: Environment.NewLine
				+ string.Join(
					Environment.NewLine,
					violations.Select((violation, index) => $"  {index + 1}. {violation.Message}")
				);
	}

	/// <summary>Drops violations a waiver excuses, so any waivable rule can be waived per project.</summary>
	/// <param name="violations">The violations to filter.</param>
	/// <returns>The violations that are not excused.</returns>
	IReadOnlyList<ArchitectureViolation> Excuse(IEnumerable<ArchitectureViolation> violations) =>
		[
			.. violations.Where(violation =>
				!ArchitectureRules.IsWaivable(violation.Rule)
				|| Policy.WaiverFor(violation.Project)?.Excuses(violation.Rule) != true
			),
		];

	/// <summary>Reports whether a project belongs to a category.</summary>
	/// <param name="projectName">The project name.</param>
	/// <param name="category">The category to test.</param>
	/// <returns><see langword="true"/> when the project's responsibility has that category.</returns>
	bool IsCategory(string projectName, string category) =>
		string.Equals(Policy.Classify(projectName)?.Category, category, StringComparison.OrdinalIgnoreCase);
}
