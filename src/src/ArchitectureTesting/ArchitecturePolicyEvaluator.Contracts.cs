using System.Text.Json;

namespace Purview.Architecture.Testing;

/// <summary>The committed-API rules: surface compatibility and documentation.</summary>
public sealed partial class ArchitecturePolicyEvaluator
{
	/// <summary>Differences between the current public API and its committed baseline.</summary>
	/// <returns>The violations.</returns>
	/// <remarks>
	/// Additions are reported as well as removals: the baseline is the reviewed contract, so growing
	/// it is a deliberate act, not an accident. A missing baseline is itself a finding, because an
	/// API rule that silently checks nothing is worse than no rule.
	/// </remarks>
	public IReadOnlyList<ArchitectureViolation> APISurfaceCompatibility()
	{
		if (!Policy.APIRules.Enabled)
			return [];

		var baselinePath = string.IsNullOrWhiteSpace(Policy.APIRules.Baseline)
			? APIBaseline.RelativePath
			: Policy.APIRules.Baseline;

		if (Baseline is null)
		{
			return
			[
				new ArchitectureViolation(
					ArchitectureRules.APISurface,
					"(repository)",
					$"No API baseline was found at '{baselinePath}'. Run 'architests api capture', review the "
						+ "result and commit it, or disable apiRules."
				),
			];
		}

		var current = APIBaseline.Capture(APISurfaces, Policy);

		return Excuse(
			APIBaselineComparer
				.Compare(Baseline, current)
				.Select(change => new ArchitectureViolation(
					ArchitectureRules.APISurface,
					change.Project,
					change.Detail
				))
		);
	}

	/// <summary>Public types and members that have no XML documentation.</summary>
	/// <returns>The violations.</returns>
	public IReadOnlyList<ArchitectureViolation> APIDocumentation()
	{
		if (!Policy.APIRules.Enabled)
			return [];

		List<ArchitectureViolation> violations = [];

		foreach (var assembly in APISurfaces.Values)
		{
			if (!IsProductionProject(assembly.ProjectName))
				continue;

			if (!XMLDocumentation.Exists(assembly.Path) && Policy.APIRules.RequireTypeDocumentation)
			{
				violations.Add(
					new ArchitectureViolation(
						ArchitectureRules.APIDocumentation,
						assembly.ProjectName,
						$"'{assembly.ProjectName}' ships no XML documentation beside its assembly. Set "
							+ "<GenerateDocumentationFile>true</GenerateDocumentationFile>, or turn off apiRules.requireTypeDocumentation."
					)
				);
				continue;
			}

			var documented = XMLDocumentation.Read(assembly.Path);

			// A compiler-synthesized container has no source to document, and its hash-based name is
			// not something an author could write a <summary> for.
			foreach (
				var type in assembly.Types.Where(candidate => candidate.IsPublic && !candidate.IsCompilerSynthesized)
			)
			{
				if (Policy.APIRules.RequireTypeDocumentation && !documented.Contains(XMLDocumentation.TypeId(type)))
				{
					violations.Add(
						new ArchitectureViolation(
							ArchitectureRules.APIDocumentation,
							assembly.ProjectName,
							$"'{type.FullName}' is public and undocumented. Add a <summary> to it."
						)
					);
				}

				if (!Policy.APIRules.RequireMemberDocumentation)
					continue;

				foreach (var member in type.Members)
				{
					if (documented.Contains(XMLDocumentation.MemberId(type, member)))
						continue;

					violations.Add(
						new ArchitectureViolation(
							ArchitectureRules.APIDocumentation,
							assembly.ProjectName,
							$"'{type.FullName}.{member.Name}' is public and undocumented. Add a <summary> to it."
						)
					);
				}
			}
		}

		return Excuse(violations);
	}

	/// <summary>The API rule identifiers enabled by the policy.</summary>
	/// <returns>The identifiers, or an empty list when the group is off.</returns>
	public IReadOnlyList<string> EnabledAPIRules() =>
		Policy.APIRules.Enabled ? [ArchitectureRules.APISurface, ArchitectureRules.APIDocumentation] : [];

	/// <summary>Evaluates one API rule by identifier.</summary>
	/// <param name="rule">The rule identifier.</param>
	/// <returns>The violations, with waived rules excused.</returns>
	public IReadOnlyList<ArchitectureViolation> EvaluateAPIRule(string rule) =>
		rule switch
		{
			ArchitectureRules.APISurface => APISurfaceCompatibility(),
			ArchitectureRules.APIDocumentation => APIDocumentation(),
			_ => [],
		};

	/// <summary>Reads the configured baseline, treating an absent or malformed file as missing.</summary>
	/// <param name="root">The repository root.</param>
	/// <param name="policy">The policy.</param>
	/// <returns>The baseline, or <see langword="null"/>.</returns>
	static APIBaseline? ReadBaseline(string root, ArchitecturePolicy policy)
	{
		var relative = string.IsNullOrWhiteSpace(policy.APIRules.Baseline)
			? APIBaseline.RelativePath
			: policy.APIRules.Baseline;

		// Resolved outside the try: a path that escapes the repository is a broken policy, not a
		// missing baseline, and the catches below would turn it into the latter.
		var path = PolicyPath.Resolve(root, relative, "apiRules.baseline");

		try
		{
			return APIBaseline.Read(path);
		}
		catch (FileNotFoundException)
		{
			return null;
		}
		catch (JsonException)
		{
			return null;
		}
		catch (InvalidOperationException)
		{
			return null;
		}
	}
}
