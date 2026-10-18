namespace Purview.Architecture.Testing;

/// <summary>Shared selection and predicate helpers for the type-level rules.</summary>
public sealed partial class ArchitecturePolicyEvaluator
{
	/// <summary>Enumerates the public types the policy governs.</summary>
	/// <returns>The project and type pairs, in assembly order.</returns>
	IEnumerable<(string Project, APIType Type)> GovernedTypes()
	{
		if (!Policy.TypeRules.Enabled)
			yield break;

		foreach (var (project, assembly) in APISurfaces)
		{
			if (!Governs(project))
				continue;

			foreach (var type in assembly.Types)
			{
				if (!type.IsPublic)
					continue;

				if (Policy.TypeRules.ExcludeGeneratedCode && type.IsGeneratedCode)
					continue;

				if (
					Policy.TypeRules.ExcludeNamespacePrefixes.Any(prefix =>
						!string.IsNullOrEmpty(prefix) && type.Namespace.StartsWith(prefix, StringComparison.Ordinal)
					)
				)
				{
					continue;
				}

				yield return (project, type);
			}
		}
	}

	/// <summary>Reports whether the type rules govern a project.</summary>
	/// <param name="projectName">The project name.</param>
	/// <returns><see langword="true"/> when the project is production code in a governed category.</returns>
	bool Governs(string projectName)
	{
		if (!Policy.TypeRules.Enabled)
			return false;

		if (!Projects.TryGetValue(projectName, out var project) || project.IsTestProject)
			return false;

		var responsibility = Policy.Classify(projectName);

		if (responsibility is null)
			return false;

		var categories = Policy.TypeRules.AppliesTo.Categories;

		return categories.Count == 0 || categories.Contains(responsibility.Category, StringComparer.OrdinalIgnoreCase);
	}

	/// <summary>Builds a type-rule violation.</summary>
	/// <param name="rule">The rule identifier.</param>
	/// <param name="project">The project that declares the type.</param>
	/// <param name="message">What is wrong and how to fix it.</param>
	/// <returns>The violation.</returns>
	static ArchitectureViolation TypeRuleViolation(string rule, string project, string message) =>
		new(rule, project, message);

	/// <summary>Reports whether a name follows the interface convention.</summary>
	/// <param name="name">The type's simple name.</param>
	/// <returns><see langword="true"/> when the name starts with <c>I</c> and an uppercase letter.</returns>
	static bool IsInterfaceName(string name) => name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]);

	/// <summary>Reports whether a declared type is a mutable collection implementation.</summary>
	/// <param name="typeName">The namespace-qualified type name.</param>
	/// <returns><see langword="true"/> for <c>List&lt;T&gt;</c> and <c>Dictionary&lt;TKey, TValue&gt;</c>.</returns>
	static bool IsMutableCollection(string typeName) =>
		typeName.StartsWith("System.Collections.Generic.List<", StringComparison.Ordinal)
		|| typeName.StartsWith("System.Collections.Generic.Dictionary<", StringComparison.Ordinal);

	/// <summary>Reports whether a type is a utility class that should be static.</summary>
	/// <param name="type">The type.</param>
	/// <returns><see langword="true"/> when it has only static members and no public constructor.</returns>
	static bool IsUtilityClass(APIType type)
	{
		var hasInstanceMember = type.Members.Any(member =>
			member.Kind != APIMemberKind.Constructor && !member.IsStatic
		);
		var hasStaticMember = type.Members.Any(member => member.Kind != APIMemberKind.Constructor && member.IsStatic);
		var hasPublicConstructor = type.Members.Any(member => member.Kind == APIMemberKind.Constructor);

		return !hasInstanceMember && hasStaticMember && !hasPublicConstructor;
	}
}
