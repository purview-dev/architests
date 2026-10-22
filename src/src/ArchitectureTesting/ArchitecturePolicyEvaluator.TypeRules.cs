namespace Purview.Architecture.Testing;

/// <summary>The type-level naming and shape rules.</summary>
public sealed partial class ArchitecturePolicyEvaluator
{
	/// <summary>Types whose names do not follow the conventions their kind implies.</summary>
	/// <returns>The violations.</returns>
	public IReadOnlyList<ArchitectureViolation> TypeNaming()
	{
		List<ArchitectureViolation> violations = [];

		foreach (var (project, type) in GovernedTypes())
		{
			if (type.Kind == APITypeKind.Interface && !IsInterfaceName(type.Name))
			{
				violations.Add(
					TypeRuleViolation(
						ArchitectureRules.TypeNaming,
						project,
						$"'{type.FullName}' is an interface whose name does not start with 'I'. "
							+ $"Rename it to 'I{type.Name}' so the kind is visible at every use."
					)
				);
			}

			if (type.IsAttribute && !type.Name.EndsWith("Attribute", StringComparison.Ordinal))
			{
				violations.Add(
					TypeRuleViolation(
						ArchitectureRules.TypeNaming,
						project,
						$"'{type.FullName}' derives from System.Attribute but does not end with 'Attribute'. "
							+ $"Rename it to '{type.Name}Attribute'."
					)
				);
			}

			if (type.IsException && !type.Name.EndsWith("Exception", StringComparison.Ordinal))
			{
				violations.Add(
					TypeRuleViolation(
						ArchitectureRules.TypeNaming,
						project,
						$"'{type.FullName}' derives from an exception but does not end with 'Exception'. "
							+ $"Rename it to '{type.Name}Exception'."
					)
				);
			}

			var suffix = Policy.TypeRules.DiscouragedTypeSuffixes.FirstOrDefault(candidate =>
				candidate.Length > 0 && type.Name.EndsWith(candidate, StringComparison.OrdinalIgnoreCase)
			);

			if (suffix is not null)
			{
				violations.Add(
					TypeRuleViolation(
						ArchitectureRules.TypeNaming,
						project,
						$"'{type.FullName}' ends with the discouraged suffix '{suffix}', which names a mechanism "
							+ "rather than a responsibility. Name what the type is for, not how it is implemented."
					)
				);
			}
		}

		return Excuse(violations);
	}

	/// <summary>Types whose declared shape weakens the boundary they sit on.</summary>
	/// <returns>The violations.</returns>
	public IReadOnlyList<ArchitectureViolation> TypeShape()
	{
		List<ArchitectureViolation> violations = [];

		foreach (var (project, type) in GovernedTypes())
		{
			if (
				Policy.TypeRules.SealConcreteClasses
				&& type.Kind == APITypeKind.Class
				&& !type.IsAbstract
				&& !type.IsSealed
			)
			{
				violations.Add(
					TypeRuleViolation(
						ArchitectureRules.TypeShape,
						project,
						$"'{type.FullName}' is a public, inheritable class. Seal it, or make it abstract if it is "
							+ "meant to be a base type: an unsealed class is a public extension point nobody declared."
					)
				);
			}

			if (
				type.Kind == APITypeKind.Class
				&& type.Name.EndsWith("Exception", StringComparison.Ordinal)
				&& !type.IsException
			)
			{
				violations.Add(
					TypeRuleViolation(
						ArchitectureRules.TypeShape,
						project,
						$"'{type.FullName}' is named like an exception but does not derive from System.Exception. "
							+ "Derive from Exception, or rename it."
					)
				);
			}

			foreach (var field in type.Members.Where(member => member.Kind == APIMemberKind.Field && !member.IsStatic))
			{
				violations.Add(
					TypeRuleViolation(
						ArchitectureRules.TypeShape,
						project,
						$"'{type.FullName}' exposes the public field '{field.Name}'. A field cannot validate or evolve "
							+ "without a breaking change; expose a property instead."
					)
				);
			}

			if (Policy.TypeRules.RequireReadOnlyCollections)
			{
				foreach (
					var member in type.Members.Where(member =>
						(member.Kind is APIMemberKind.Property or APIMemberKind.Method)
						&& IsMutableCollection(member.TypeName)
					)
				)
				{
					violations.Add(
						TypeRuleViolation(
							ArchitectureRules.TypeShape,
							project,
							$"'{type.FullName}.{member.Name}' exposes '{member.TypeName}'. Return IReadOnlyList<T> or "
								+ "IReadOnlyDictionary<TKey, TValue> so a caller cannot mutate the owner's state."
						)
					);
				}
			}

			if (type.Kind == APITypeKind.Class && !type.IsStatic && !type.IsAbstract && IsUtilityClass(type))
			{
				violations.Add(
					TypeRuleViolation(
						ArchitectureRules.TypeShape,
						project,
						$"'{type.FullName}' has only static members and no public constructor. Declare it static so "
							+ "it cannot be instantiated by accident."
					)
				);
			}
		}

		return Excuse(violations);
	}
}
