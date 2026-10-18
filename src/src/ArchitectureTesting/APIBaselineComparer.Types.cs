namespace Purview.Architecture.Testing;

/// <summary>Compares the shape and members of one type.</summary>
public static partial class APIBaselineComparer
{
	/// <summary>Compares one type's shape and members.</summary>
	/// <param name="project">The project name.</param>
	/// <param name="expected">The baseline type.</param>
	/// <param name="current">The captured type.</param>
	/// <param name="changes">The accumulating differences.</param>
	static void CompareType(
		string project,
		APIBaselineType expected,
		APIBaselineType current,
		List<APIBaselineChange> changes
	)
	{
		List<string> details = [];

		if (!string.Equals(expected.Kind, current.Kind, StringComparison.Ordinal))
			details.Add($"kind changed from {expected.Kind} to {current.Kind}");

		if (expected.IsAbstract != current.IsAbstract)
			details.Add($"abstract changed from {expected.IsAbstract} to {current.IsAbstract}");

		if (expected.IsSealed != current.IsSealed)
			details.Add($"sealed changed from {expected.IsSealed} to {current.IsSealed}");

		if (!string.Equals(expected.BaseType, current.BaseType, StringComparison.Ordinal))
		{
			details.Add(
				$"base type changed from '{expected.BaseType ?? "(none)"}' to '{current.BaseType ?? "(none)"}'"
			);
		}

		var removedInterfaces = expected.Interfaces.Except(current.Interfaces, StringComparer.Ordinal).ToArray();
		var addedInterfaces = current.Interfaces.Except(expected.Interfaces, StringComparer.Ordinal).ToArray();

		if (removedInterfaces.Length > 0)
			details.Add($"interfaces removed: {string.Join(", ", removedInterfaces)}");

		if (addedInterfaces.Length > 0)
			details.Add($"interfaces added: {string.Join(", ", addedInterfaces)}");

		if (details.Count > 0)
		{
			changes.Add(
				new APIBaselineChange(
					APIBaselineChangeKind.Changed,
					project,
					expected.Name,
					$"'{expected.Name}' changed: {string.Join("; ", details)}. "
						+ "Run 'architests api capture' after reviewing the change."
				)
			);
		}

		CompareMembers(project, expected, current, changes);
	}

	/// <summary>Compares one type's members, matching overloads by kind, name and parameters.</summary>
	/// <param name="project">The project name.</param>
	/// <param name="expected">The baseline type.</param>
	/// <param name="current">The captured type.</param>
	/// <param name="changes">The accumulating differences.</param>
	static void CompareMembers(
		string project,
		APIBaselineType expected,
		APIBaselineType current,
		List<APIBaselineChange> changes
	)
	{
		var expectedMembers = expected.Members.ToDictionary(MemberKey, StringComparer.Ordinal);
		var actualMembers = current.Members.ToDictionary(MemberKey, StringComparer.Ordinal);

		foreach (var key in expectedMembers.Keys.Except(actualMembers.Keys, StringComparer.Ordinal))
		{
			var subject = MemberSubject(expected.Name, expectedMembers[key]);

			changes.Add(
				new APIBaselineChange(
					APIBaselineChangeKind.Removed,
					project,
					subject,
					$"'{subject}' is gone. This is a breaking change: restore it, or capture the baseline as a "
						+ "deliberate breaking release."
				)
			);
		}

		foreach (var key in actualMembers.Keys.Except(expectedMembers.Keys, StringComparer.Ordinal))
		{
			var subject = MemberSubject(expected.Name, actualMembers[key]);

			changes.Add(
				new APIBaselineChange(
					APIBaselineChangeKind.Added,
					project,
					subject,
					$"'{subject}' is new public API. Run 'architests api capture' and review it before committing."
				)
			);
		}

		foreach (var key in expectedMembers.Keys.Intersect(actualMembers.Keys, StringComparer.Ordinal))
		{
			var before = expectedMembers[key];
			var after = actualMembers[key];

			if (
				string.Equals(before.TypeName, after.TypeName, StringComparison.Ordinal)
				&& before.IsStatic == after.IsStatic
			)
			{
				continue;
			}

			var detail = !string.Equals(before.TypeName, after.TypeName, StringComparison.Ordinal)
				? $"type changed from '{before.TypeName}' to '{after.TypeName}'"
				: $"static changed from {before.IsStatic} to {after.IsStatic}";

			var subject = MemberSubject(expected.Name, before);

			changes.Add(
				new APIBaselineChange(
					APIBaselineChangeKind.Changed,
					project,
					subject,
					$"'{subject}' changed: {detail}. Run 'architests api capture' after reviewing the change."
				)
			);
		}
	}

	/// <summary>Builds the identity of a member, so an overload is not confused with its sibling.</summary>
	/// <param name="member">The member.</param>
	/// <returns>The identity.</returns>
	static string MemberKey(APIBaselineMember member) =>
		$"{member.Kind}|{member.Name}|{string.Join(",", member.Parameters)}";

	/// <summary>Renders a member for a failure message, including its parameters.</summary>
	/// <param name="typeName">The declaring type.</param>
	/// <param name="member">The member.</param>
	/// <returns>The subject.</returns>
	static string MemberSubject(string typeName, APIBaselineMember member) =>
		member.Parameters.Count == 0
			? $"{typeName}.{member.Name}"
			: $"{typeName}.{member.Name}({string.Join(", ", member.Parameters)})";
}
