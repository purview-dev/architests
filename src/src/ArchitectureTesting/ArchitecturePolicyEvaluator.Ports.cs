namespace Purview.Architecture.Testing;

/// <summary>The port rules: implementation and technology purity.</summary>
public sealed partial class ArchitecturePolicyEvaluator
{
	/// <summary>Ports with no implementation in the repository.</summary>
	/// <returns>The violations.</returns>
	/// <remarks>
	/// A port is an interface in one of <see cref="PortRules.Categories"/>: a contract the inner
	/// layers own. An interface nobody implements is either dead or a boundary that was never
	/// completed, so it is reported rather than left as decoration.
	/// <para>
	/// An <c>internal</c> adapter counts. The boundary is crossed whether or not a consumer outside
	/// the assembly can see the type doing the crossing, and a composition root wiring an internal
	/// implementation is the ordinary shape. Set
	/// <see cref="PortRules.RequirePublicImplementation"/> to demand a public one.
	/// </para>
	/// </remarks>
	public IReadOnlyList<ArchitectureViolation> PortImplementation()
	{
		if (!Policy.PortRules.Enabled)
			return [];

		var requirePublic = Policy.PortRules.RequirePublicImplementation;

		List<ArchitectureViolation> violations = [];

		foreach (var (project, port) in Ports())
		{
			var implemented = APISurfaces
				.Values.Where(assembly => IsProductionProject(assembly.ProjectName))
				.SelectMany(assembly => assembly.Types)
				.Any(type =>
					(type.IsPublic || !requirePublic)
					&& type.Kind != APITypeKind.Interface
					&& type.Interfaces.Contains(port.FullName, StringComparer.Ordinal)
				);

			if (implemented)
				continue;

			var responsibility = Policy.Classify(project);
			var visibility = requirePublic ? "public implementation" : "implementation";

			violations.Add(
				new ArchitectureViolation(
					ArchitectureRules.PortImplementation,
					project,
					$"'{port.FullName}' is a port in {responsibility?.Name ?? project} with no {visibility} in the repository. "
						+ "Implement it in an adapter, or remove it: an unimplemented contract is a boundary nobody crossed."
				)
			);
		}

		return Excuse(violations);
	}

	/// <summary>Ports that expose an infrastructure technology in their signatures.</summary>
	/// <returns>The violations.</returns>
	public IReadOnlyList<ArchitectureViolation> PortPurity()
	{
		if (!Policy.PortRules.Enabled)
			return [];

		List<ArchitectureViolation> violations = [];

		foreach (var (project, port) in Ports())
		{
			foreach (var member in port.Members)
			{
				var exposed = member.Parameters.Prepend(member.TypeName);
				var leak = exposed
					.Select(type =>
						(
							Type: type,
							// A prefix, as the setting is named and documented. Contains() also matched
							// a type whose *namespace* merely contained the prefix part-way through,
							// so 'System.Data' flagged 'Contoso.System.DataPoint'.
							//
							// Matched inside generic arguments too: IReadOnlyList<DbContext> exposes
							// the technology just as plainly as returning it directly.
							Prefix: Policy.PortRules.ForbiddenTypePrefixes.FirstOrDefault(prefix =>
								!string.IsNullOrEmpty(prefix) && NamesPrefix(type, prefix)
							)
						)
					)
					.FirstOrDefault(candidate => candidate.Prefix is not null);

				if (leak.Prefix is null)
					continue;

				violations.Add(
					new ArchitectureViolation(
						ArchitectureRules.PortPurity,
						project,
						$"'{port.FullName}.{member.Name}' exposes '{leak.Type}', which names the infrastructure "
							+ $"prefix '{leak.Prefix}'. A port must be technology-neutral; move the technology into the adapter."
					)
				);
			}
		}

		return Excuse(violations);
	}

	/// <summary>The port rule identifiers enabled by the policy.</summary>
	/// <returns>The identifiers, or an empty list when the group is off.</returns>
	public IReadOnlyList<string> EnabledPortRules() =>
		Policy.PortRules.Enabled ? [ArchitectureRules.PortImplementation, ArchitectureRules.PortPurity] : [];

	/// <summary>Evaluates one port rule by identifier.</summary>
	/// <param name="rule">The rule identifier.</param>
	/// <returns>The violations, with waived rules excused.</returns>
	public IReadOnlyList<ArchitectureViolation> EvaluatePortRule(string rule) =>
		rule switch
		{
			ArchitectureRules.PortImplementation => PortImplementation(),
			ArchitectureRules.PortPurity => PortPurity(),
			_ => [],
		};

	/// <summary>Reports whether a rendered type name, or any of its generic arguments, starts with a prefix.</summary>
	/// <param name="typeName">The rendered type name, such as <c>IReadOnlyList&lt;Ns.Order&gt;</c>.</param>
	/// <param name="prefix">The forbidden namespace prefix.</param>
	/// <returns><see langword="true"/> when the prefix begins the type or one of its arguments.</returns>
	/// <remarks>
	/// A signature renders as a single string, so the generic arguments are split out and each part
	/// tested on its own. The alternative - a substring search over the whole string - matches a
	/// prefix appearing anywhere, including part-way through an unrelated namespace.
	/// </remarks>
	static bool NamesPrefix(string typeName, string prefix) =>
		typeName
			.Split(['<', '>', ',', '[', ']'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Any(part => part.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

	/// <summary>Enumerates the interfaces the policy treats as ports.</summary>
	/// <returns>The declaring project and the interface.</returns>
	IEnumerable<(string Project, APIType Type)> Ports()
	{
		if (!Policy.PortRules.Enabled)
			yield break;

		foreach (var (project, assembly) in APISurfaces)
		{
			if (!IsProductionProject(project))
				continue;

			var responsibility = Policy.Classify(project);

			if (
				responsibility is null
				|| !Policy.PortRules.Categories.Contains(responsibility.Category, StringComparer.OrdinalIgnoreCase)
			)
			{
				continue;
			}

			foreach (
				var type in assembly.Types.Where(candidate =>
					candidate.IsPublic && candidate.Kind == APITypeKind.Interface
				)
			)
			{
				yield return (project, type);
			}
		}
	}

	/// <summary>Reports whether a project is production code.</summary>
	/// <param name="projectName">The project name.</param>
	/// <returns><see langword="true"/> when the project exists and is not a test project.</returns>
	bool IsProductionProject(string projectName) =>
		Projects.TryGetValue(projectName, out var project) && !project.IsTestProject;
}
