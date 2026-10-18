namespace Purview.Architecture.Testing;

/// <summary>The attribute rule.</summary>
public sealed partial class ArchitecturePolicyEvaluator
{
	/// <summary>Types and members whose attributes are missing, forbidden or misleading.</summary>
	/// <returns>The violations.</returns>
	public IReadOnlyList<ArchitectureViolation> AttributePolicy()
	{
		List<ArchitectureViolation> violations = [];
		var settings = Policy.TypeRules;

		foreach (var (project, type) in GovernedTypes())
		{
			foreach (var required in settings.RequiredAttributes)
			{
				if (!HasAttribute(type.Attributes, required))
				{
					violations.Add(
						TypeRuleViolation(
							ArchitectureRules.AttributePolicy,
							project,
							$"'{type.FullName}' is missing the required attribute '{required}'."
						)
					);
				}
			}

			foreach (var forbidden in settings.ForbiddenAttributes)
			{
				if (HasAttribute(type.Attributes, forbidden))
				{
					violations.Add(
						TypeRuleViolation(
							ArchitectureRules.AttributePolicy,
							project,
							$"'{type.FullName}' carries the forbidden attribute '{forbidden}'."
						)
					);
				}
			}

			if (settings.AllowObsoleteWithoutMessage)
				continue;

			AddObsolete(violations, project, $"type '{type.FullName}'", type.Attributes);

			foreach (var member in type.Members)
			{
				AddObsolete(violations, project, $"member '{type.FullName}.{member.Name}'", member.Attributes);
			}
		}

		if (settings.ForbidInternalsVisibleToProduction)
			violations.AddRange(InternalsVisibleToProduction());

		return Excuse(violations);
	}

	/// <summary>Reports friend assemblies that expose internals to production code.</summary>
	/// <returns>The violations.</returns>
	IEnumerable<ArchitectureViolation> InternalsVisibleToProduction()
	{
		foreach (var (project, assembly) in APISurfaces)
		{
			if (!Governs(project))
				continue;

			foreach (
				var attribute in assembly.Attributes.Where(candidate =>
					candidate.TypeName == "System.Runtime.CompilerServices.InternalsVisibleToAttribute"
				)
			)
			{
				// A strong-named friend is written "Name, PublicKey=..."; the name is what identifies it.
				var friend = attribute.StringArgument?.Split(',')[0].Trim();

				if (string.IsNullOrEmpty(friend) || !IsProductionAssembly(friend))
					continue;

				yield return TypeRuleViolation(
					ArchitectureRules.AttributePolicy,
					project,
					$"assembly '{assembly.AssemblyName}' makes its internals visible to the production assembly "
						+ $"'{friend}'. Expose a public abstraction instead, or restrict the friend to a test project."
				);
			}
		}
	}

	/// <summary>Reports whether an assembly name belongs to a production project.</summary>
	/// <param name="assemblyName">The friend assembly's simple name.</param>
	/// <returns><see langword="true"/> when it maps to a production project.</returns>
	bool IsProductionAssembly(string assemblyName)
	{
		var projectName =
			APISurfaces
				.Values.FirstOrDefault(assembly =>
					string.Equals(assembly.AssemblyName, assemblyName, StringComparison.OrdinalIgnoreCase)
				)
				?.ProjectName
			?? Assemblies
				.Values.FirstOrDefault(assembly =>
					string.Equals(assembly.AssemblyName, assemblyName, StringComparison.OrdinalIgnoreCase)
				)
				?.ProjectName
			?? Projects
				.Values.FirstOrDefault(project =>
					string.Equals(project.Name, assemblyName, StringComparison.OrdinalIgnoreCase)
				)
				?.Name;

		return projectName is not null && Projects.TryGetValue(projectName, out var project) && !project.IsTestProject;
	}

	/// <summary>Adds a violation for an <c>[Obsolete]</c> without a message.</summary>
	/// <param name="violations">The accumulating violations.</param>
	/// <param name="project">The project.</param>
	/// <param name="subject">The type or member the attribute is applied to.</param>
	/// <param name="attributes">The attributes applied to it.</param>
	static void AddObsolete(
		List<ArchitectureViolation> violations,
		string project,
		string subject,
		IReadOnlyList<APIAttribute> attributes
	)
	{
		foreach (
			var attribute in attributes.Where(candidate =>
				candidate.TypeName == "System.ObsoleteAttribute" && string.IsNullOrWhiteSpace(candidate.StringArgument)
			)
		)
		{
			violations.Add(
				TypeRuleViolation(
					ArchitectureRules.AttributePolicy,
					project,
					$"{subject} is marked [Obsolete] without a message. Say what to use instead, so the caller can act."
				)
			);
		}
	}

	/// <summary>Reports whether a type carries an attribute.</summary>
	/// <param name="attributes">The attributes applied to the type.</param>
	/// <param name="typeName">The namespace-qualified attribute type name.</param>
	/// <returns><see langword="true"/> when the attribute is present.</returns>
	static bool HasAttribute(IReadOnlyList<APIAttribute> attributes, string typeName) =>
		attributes.Any(attribute => string.Equals(attribute.TypeName, typeName, StringComparison.Ordinal));
}
