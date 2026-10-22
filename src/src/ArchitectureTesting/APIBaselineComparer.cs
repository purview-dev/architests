namespace Purview.Architecture.Testing;

/// <summary>How the current public API differs from its baseline.</summary>
public enum APIBaselineChangeKind
{
	/// <summary>API exists in the build but not in the baseline.</summary>
	Added,

	/// <summary>API exists in the baseline but not in the build.</summary>
	Removed,

	/// <summary>API exists in both, but its shape changed.</summary>
	Changed,
}

/// <summary>One difference between a baseline and the current public API.</summary>
/// <param name="Kind">Whether the API was added, removed or changed.</param>
/// <param name="Project">The project whose API differs.</param>
/// <param name="Subject">The type or member that differs.</param>
/// <param name="Detail">What changed and what to do about it.</param>
public sealed record APIBaselineChange(APIBaselineChangeKind Kind, string Project, string Subject, string Detail)
{
	/// <inheritdoc />
	public override string ToString() => Detail;
}

/// <summary>Compares a committed baseline with the API a build produced.</summary>
public static partial class APIBaselineComparer
{
	/// <summary>Compares two baselines.</summary>
	/// <param name="baseline">The committed baseline.</param>
	/// <param name="current">The API captured from the current build.</param>
	/// <returns>The differences, with removals first.</returns>
	public static IReadOnlyList<APIBaselineChange> Compare(APIBaseline baseline, APIBaseline current)
	{
		ArgumentNullException.ThrowIfNull(baseline);
		ArgumentNullException.ThrowIfNull(current);

		List<APIBaselineChange> changes = [];

		var expected = baseline.Projects.ToDictionary(project => project.Project, StringComparer.OrdinalIgnoreCase);
		var actual = current.Projects.ToDictionary(project => project.Project, StringComparer.OrdinalIgnoreCase);

		foreach (var project in expected.Keys.Except(actual.Keys, StringComparer.OrdinalIgnoreCase))
		{
			changes.Add(
				new APIBaselineChange(
					APIBaselineChangeKind.Removed,
					project,
					project,
					$"'{project}' contributes no public API to the build, but the baseline records it. "
						+ "Restore the assembly, or run 'architests api capture' if the removal is intended."
				)
			);
		}

		foreach (var project in actual.Keys.Except(expected.Keys, StringComparer.OrdinalIgnoreCase))
		{
			changes.Add(
				new APIBaselineChange(
					APIBaselineChangeKind.Added,
					project,
					project,
					$"'{project}' contributes public API the baseline does not record. "
						+ "Run 'architests api capture' and review the added surface before committing it."
				)
			);
		}

		foreach (var project in expected.Keys.Intersect(actual.Keys, StringComparer.OrdinalIgnoreCase))
		{
			CompareProject(expected[project], actual[project], changes);
		}

		return changes;
	}

	/// <summary>Compares one project's types.</summary>
	/// <param name="expected">The baseline project.</param>
	/// <param name="current">The captured project.</param>
	/// <param name="changes">The accumulating differences.</param>
	static void CompareProject(APIBaselineProject expected, APIBaselineProject current, List<APIBaselineChange> changes)
	{
		var expectedTypes = expected.Types.ToDictionary(type => type.Name, StringComparer.Ordinal);
		var actualTypes = current.Types.ToDictionary(type => type.Name, StringComparer.Ordinal);

		foreach (var name in expectedTypes.Keys.Except(actualTypes.Keys, StringComparer.Ordinal))
		{
			changes.Add(
				new APIBaselineChange(
					APIBaselineChangeKind.Removed,
					expected.Project,
					name,
					$"'{name}' was public and is gone. This is a breaking change: restore it, or capture the "
						+ "baseline as a deliberate breaking release."
				)
			);
		}

		foreach (var name in actualTypes.Keys.Except(expectedTypes.Keys, StringComparer.Ordinal))
		{
			changes.Add(
				new APIBaselineChange(
					APIBaselineChangeKind.Added,
					expected.Project,
					name,
					$"'{name}' is new public API. Run 'architests api capture' and review it before committing."
				)
			);
		}

		foreach (var name in expectedTypes.Keys.Intersect(actualTypes.Keys, StringComparer.Ordinal))
		{
			CompareType(expected.Project, expectedTypes[name], actualTypes[name], changes);
		}
	}
}
