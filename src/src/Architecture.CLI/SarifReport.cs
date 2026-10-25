using System.Text.Json;
using Purview.Architecture.Testing;

namespace Purview.Architecture.CLI;

/// <summary>Renders findings as a SARIF 2.1.0 document.</summary>
/// <remarks>
/// SARIF is what makes the tool usable outside its own output: GitHub code scanning, an IDE and a
/// pull-request annotation all read it, so a violation becomes a comment on the line rather than a
/// line in a log nobody opens.
/// <para>
/// The <c>rules</c> array is built from <see cref="ArchitectureRules.Catalogue"/>, so a reader gets
/// the rule's title and description without this file keeping its own copy. A rule added to the
/// catalogue appears here with no further change.
/// </para>
/// </remarks>
static class SarifReport
{
	static readonly JsonSerializerOptions Options = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	/// <summary>Renders architecture violations.</summary>
	/// <param name="violations">The violations.</param>
	/// <param name="root">The repository root, which artifact URIs are relative to.</param>
	/// <param name="projects">The project graph, used to point a finding at its project file.</param>
	/// <returns>The SARIF document.</returns>
	public static string Render(
		IReadOnlyList<ArchitectureViolation> violations,
		string root,
		IReadOnlyDictionary<string, ProjectNode> projects
	)
	{
		var results = violations
			.Select(violation =>
				Result(
					violation.Rule,
					// A rule that is not waivable is a guard on the policy itself rather than a style
					// preference, so it is reported at the higher level.
					ArchitectureRules.IsWaivable(violation.Rule)
						? "warning"
						: "error",
					violation.Message,
					LocationFor(violation, root, projects)
				)
			)
			.ToArray();

		return Render(results, Rules(violations.Select(violation => violation.Rule)));
	}

	/// <summary>Renders API baseline differences.</summary>
	/// <param name="changes">The differences.</param>
	/// <param name="root">The repository root, which artifact URIs are relative to.</param>
	/// <param name="baselinePath">The baseline file the differences are measured against.</param>
	/// <returns>The SARIF document.</returns>
	public static string Render(IReadOnlyList<APIBaselineChange> changes, string root, string baselinePath)
	{
		var uri = RelativeUri(root, baselinePath);

		var results = changes
			.Select(change =>
				Result(
					ArchitectureRules.APISurface,
					// Removing public API breaks a consumer at compile time; adding to it is a review
					// item. The two are not the same severity.
					change.Kind == APIBaselineChangeKind.Removed
						? "error"
						: "warning",
					change.Detail,
					uri
				)
			)
			.ToArray();

		return Render(results, Rules([ArchitectureRules.APISurface]));
	}

	/// <summary>Builds one SARIF result.</summary>
	/// <param name="ruleId">The rule identifier.</param>
	/// <param name="level">The SARIF level.</param>
	/// <param name="message">The finding, as the reader should see it.</param>
	/// <param name="uri">The repo-relative artifact URI, or empty when there is none.</param>
	/// <returns>The result.</returns>
	static Dictionary<string, object?> Result(string ruleId, string level, string message, string uri)
	{
		Dictionary<string, object?> result = new()
		{
			["ruleId"] = ruleId,
			["level"] = level,
			["message"] = new Dictionary<string, object?> { ["text"] = message },
		};

		// A result with no location is valid SARIF, and is better than inventing one: a finding
		// about the policy as a whole does not belong on an arbitrary file.
		if (uri.Length > 0)
		{
			result["locations"] = new[]
			{
				new Dictionary<string, object?>
				{
					["physicalLocation"] = new Dictionary<string, object?>
					{
						["artifactLocation"] = new Dictionary<string, object?> { ["uri"] = uri },
					},
				},
			};
		}

		return result;
	}

	/// <summary>Builds the rule metadata for the rules a run actually reported.</summary>
	/// <param name="ruleIds">The rule identifiers reported.</param>
	/// <returns>The rule descriptors.</returns>
	static Dictionary<string, object?>[] Rules(IEnumerable<string> ruleIds)
	{
		var reported = ruleIds.ToHashSet(StringComparer.Ordinal);

		return
		[
			.. ArchitectureRules
				.Catalogue.Where(descriptor => reported.Contains(descriptor.Rule))
				.Select(descriptor => new Dictionary<string, object?>
				{
					["id"] = descriptor.Rule,
					["name"] = descriptor.Title,
					["shortDescription"] = new Dictionary<string, object?> { ["text"] = descriptor.Title },
					["fullDescription"] = new Dictionary<string, object?> { ["text"] = descriptor.Summary },
					["properties"] = new Dictionary<string, object?>
					{
						["dataSource"] = descriptor.DataSource,
						["waivable"] = descriptor.Waivable,
					},
				}),
		];
	}

	/// <summary>Wraps results and rules in the SARIF envelope.</summary>
	/// <param name="results">The results.</param>
	/// <param name="rules">The rule metadata.</param>
	/// <returns>The SARIF document.</returns>
	static string Render(Dictionary<string, object?>[] results, Dictionary<string, object?>[] rules)
	{
		Dictionary<string, object?> document = new()
		{
			["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
			["version"] = "2.1.0",
			["runs"] = new[]
			{
				new Dictionary<string, object?>
				{
					["tool"] = new Dictionary<string, object?>
					{
						["driver"] = new Dictionary<string, object?>
						{
							["name"] = "architests",
							["version"] = CommandLine.Version,
							["informationUri"] = "https://github.com/purview-dev/architests",
							["rules"] = rules,
						},
					},
					["results"] = results,
				},
			},
		};

		return JsonSerializer.Serialize(document, Options);
	}

	/// <summary>Finds the file a violation should be reported against.</summary>
	/// <param name="violation">The violation.</param>
	/// <param name="root">The repository root.</param>
	/// <param name="projects">The project graph.</param>
	/// <returns>The repo-relative URI, or empty when the violation names no project file.</returns>
	/// <remarks>
	/// The project file is the closest thing to a location the evaluator has: it reads metadata and
	/// project references, not source, so there is no line to point at. Naming the <c>.csproj</c>
	/// still puts the finding on the right project in a pull request.
	/// </remarks>
	static string LocationFor(
		ArchitectureViolation violation,
		string root,
		IReadOnlyDictionary<string, ProjectNode> projects
	) =>
		projects.TryGetValue(violation.Project, out var project) && project.Path.Length > 0
			? RelativeUri(root, project.Path)
			: string.Empty;

	/// <summary>Renders a path as a repo-relative, forward-slashed URI.</summary>
	/// <param name="root">The repository root.</param>
	/// <param name="path">The absolute path.</param>
	/// <returns>The URI.</returns>
	static string RelativeUri(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/');
}
