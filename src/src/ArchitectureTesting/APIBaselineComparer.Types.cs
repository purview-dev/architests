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

	/// <summary>Compares one type's members, matching overloads by kind, name, arity and parameters.</summary>
	/// <param name="project">The project name.</param>
	/// <param name="expected">The baseline type.</param>
	/// <param name="current">The captured type.</param>
	/// <param name="changes">The accumulating differences.</param>
	/// <remarks>
	/// Members are grouped rather than keyed into a dictionary. Overload identity is not unique on
	/// its own - C# permits members that differ only in their declared type - so building a
	/// dictionary from it throws on legal API rather than reporting drift.
	/// </remarks>
	static void CompareMembers(
		string project,
		APIBaselineType expected,
		APIBaselineType current,
		List<APIBaselineChange> changes
	)
	{
		var expectedOverloads = GroupByOverload(expected.Members);
		var actualOverloads = GroupByOverload(current.Members);

		foreach (var key in expectedOverloads.Keys.Except(actualOverloads.Keys, StringComparer.Ordinal))
			Removed(project, expected.Name, expectedOverloads[key], changes);

		foreach (var key in actualOverloads.Keys.Except(expectedOverloads.Keys, StringComparer.Ordinal))
			Added(project, expected.Name, actualOverloads[key], changes);

		foreach (var key in expectedOverloads.Keys.Intersect(actualOverloads.Keys, StringComparer.Ordinal))
			CompareOverloads(project, expected.Name, expectedOverloads[key], actualOverloads[key], changes);
	}

	/// <summary>Compares the members that share one overload identity.</summary>
	/// <param name="project">The project name.</param>
	/// <param name="typeName">The declaring type.</param>
	/// <param name="expected">The baseline members.</param>
	/// <param name="current">The captured members.</param>
	/// <param name="changes">The accumulating differences.</param>
	/// <remarks>
	/// The common case is one member on each side, where a difference is reported as a change rather
	/// than as a removal and an addition. Where the declared type is what separates two siblings,
	/// they are matched by it instead, and whatever is left over is a genuine removal or addition.
	/// </remarks>
	static void CompareOverloads(
		string project,
		string typeName,
		List<APIBaselineMember> expected,
		List<APIBaselineMember> current,
		List<APIBaselineChange> changes
	)
	{
		if (expected.Count == 1 && current.Count == 1)
		{
			CompareMember(project, typeName, expected[0], current[0], changes);
			return;
		}

		ReportDuplicates(project, typeName, expected, changes);
		ReportDuplicates(project, typeName, current, changes);

		List<APIBaselineMember> unmatched = [.. current];

		foreach (var before in expected)
		{
			var index = unmatched.FindIndex(after =>
				string.Equals(after.Signature, before.Signature, StringComparison.Ordinal)
			);

			if (index < 0)
			{
				Removed(project, typeName, [before], changes);
				continue;
			}

			CompareMember(project, typeName, before, unmatched[index], changes);
			unmatched.RemoveAt(index);
		}

		Added(project, typeName, unmatched, changes);
	}

	/// <summary>Compares one matched pair of members.</summary>
	/// <param name="project">The project name.</param>
	/// <param name="typeName">The declaring type.</param>
	/// <param name="before">The baseline member.</param>
	/// <param name="after">The captured member.</param>
	/// <param name="changes">The accumulating differences.</param>
	static void CompareMember(
		string project,
		string typeName,
		APIBaselineMember before,
		APIBaselineMember after,
		List<APIBaselineChange> changes
	)
	{
		var typeChanged = !string.Equals(before.TypeName, after.TypeName, StringComparison.Ordinal);

		if (!typeChanged && before.IsStatic == after.IsStatic)
			return;

		var detail = typeChanged
			? $"type changed from '{before.TypeName}' to '{after.TypeName}'"
			: $"static changed from {before.IsStatic} to {after.IsStatic}";

		var subject = MemberSubject(typeName, before);

		changes.Add(
			new APIBaselineChange(
				APIBaselineChangeKind.Changed,
				project,
				subject,
				$"'{subject}' changed: {detail}. Run 'architests api capture' after reviewing the change."
			)
		);
	}

	/// <summary>Reports members the build no longer declares.</summary>
	/// <param name="project">The project name.</param>
	/// <param name="typeName">The declaring type.</param>
	/// <param name="members">The members.</param>
	/// <param name="changes">The accumulating differences.</param>
	static void Removed(
		string project,
		string typeName,
		IEnumerable<APIBaselineMember> members,
		List<APIBaselineChange> changes
	)
	{
		foreach (var member in members)
		{
			var subject = MemberSubject(typeName, member);

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
	}

	/// <summary>Reports members the baseline does not record.</summary>
	/// <param name="project">The project name.</param>
	/// <param name="typeName">The declaring type.</param>
	/// <param name="members">The members.</param>
	/// <param name="changes">The accumulating differences.</param>
	static void Added(
		string project,
		string typeName,
		IEnumerable<APIBaselineMember> members,
		List<APIBaselineChange> changes
	)
	{
		foreach (var member in members)
		{
			var subject = MemberSubject(typeName, member);

			changes.Add(
				new APIBaselineChange(
					APIBaselineChangeKind.Added,
					project,
					subject,
					$"'{subject}' is new public API. Run 'architests api capture' and review it before committing."
				)
			);
		}
	}

	/// <summary>Reports members that cannot be told apart.</summary>
	/// <param name="project">The project name.</param>
	/// <param name="typeName">The declaring type.</param>
	/// <param name="members">The members sharing one overload identity.</param>
	/// <param name="changes">The accumulating differences.</param>
	/// <remarks>
	/// Real metadata cannot declare the same full signature twice, so a duplicate means the baseline
	/// has been hand-edited or merged badly. It is reported rather than thrown, because a comparison
	/// that stops at the first oddity tells the reader nothing about the rest of the surface.
	/// </remarks>
	static void ReportDuplicates(
		string project,
		string typeName,
		List<APIBaselineMember> members,
		List<APIBaselineChange> changes
	)
	{
		foreach (
			var duplicate in members
				.GroupBy(member => member.Signature, StringComparer.Ordinal)
				.Where(group => group.Count() > 1)
		)
		{
			var subject = MemberSubject(typeName, duplicate.First());

			changes.Add(
				new APIBaselineChange(
					APIBaselineChangeKind.Changed,
					project,
					subject,
					$"'{subject}' is declared {duplicate.Count()} times with an identical signature, which "
						+ "metadata cannot produce. Run 'architests api capture' to rewrite the baseline."
				)
			);
		}
	}

	/// <summary>Groups members by overload identity, preserving declaration order within a group.</summary>
	/// <param name="members">The members.</param>
	/// <returns>The members, keyed by overload identity.</returns>
	static Dictionary<string, List<APIBaselineMember>> GroupByOverload(IEnumerable<APIBaselineMember> members)
	{
		Dictionary<string, List<APIBaselineMember>> groups = new(StringComparer.Ordinal);

		foreach (var member in members)
		{
			if (!groups.TryGetValue(member.OverloadKey, out var group))
				groups[member.OverloadKey] = group = [];

			group.Add(member);
		}

		return groups;
	}

	/// <summary>Renders a member for a failure message, including its arity and parameters.</summary>
	/// <param name="typeName">The declaring type.</param>
	/// <param name="member">The member.</param>
	/// <returns>The subject.</returns>
	static string MemberSubject(string typeName, APIBaselineMember member)
	{
		var parameters = member.Parameters.Count == 0 ? string.Empty : $"({string.Join(", ", member.Parameters)})";

		return $"{typeName}.{member.Name}{TypeParameters(member.GenericArity)}{parameters}";
	}

	/// <summary>Renders a member's type parameter list, so an overload is named unambiguously.</summary>
	/// <param name="arity">The number of generic parameters.</param>
	/// <returns>The rendered list, or empty when the member is not generic.</returns>
	static string TypeParameters(int arity) =>
		arity switch
		{
			0 => string.Empty,
			1 => "<T>",
			_ => $"<{string.Join(", ", Enumerable.Range(1, arity).Select(index => $"T{index}"))}>",
		};
}
