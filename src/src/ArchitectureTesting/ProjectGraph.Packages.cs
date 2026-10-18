using System.Text.Json;

namespace Purview.Architecture.Testing;

/// <summary>Reads the restore package graph from <c>project.assets.json</c>.</summary>
public static partial class ProjectGraph
{
	/// <summary>Reads the restore graph for each target framework of a project.</summary>
	/// <param name="projectPath">The project file path.</param>
	/// <returns>The targets, or an empty list before a restore.</returns>
	/// <remarks>
	/// The project file shows only a direct <c>PackageReference</c>. Restore's assets file records
	/// the full graph, including a package injected by <c>Directory.Build.props</c>, a package that
	/// only arrives transitively, and the packages each referenced project contributes. It is
	/// restore output rather than MSBuild, so the reader stays free of build evaluation.
	/// </remarks>
	static List<PackageTarget> ReadPackageTargets(string projectPath)
	{
		var directory = Path.GetDirectoryName(Path.GetFullPath(projectPath));
		var assetsPath = Path.Combine(directory ?? ".", "obj", "project.assets.json");

		if (!File.Exists(assetsPath))
			return [];

		try
		{
			using var document = JsonDocument.Parse(File.ReadAllBytes(assetsPath));
			var root = document.RootElement;

			if (!root.TryGetProperty("targets", out var targets) || targets.ValueKind != JsonValueKind.Object)
			{
				return [];
			}

			var direct = ReadDirectPackages(root);
			List<PackageTarget> result = [];

			foreach (var target in targets.EnumerateObject())
			{
				if (target.Value.ValueKind != JsonValueKind.Object)
					continue;

				var nodes = ReadNodes(target.Value);

				if (nodes.Count == 0)
					continue;

				var resolved = nodes
					.Values.Where(node => node.IsPackage)
					.Select(node => node.Id)
					.ToHashSet(StringComparer.OrdinalIgnoreCase);

				var owned = Closure(direct, nodes, includeProjects: false);

				Dictionary<string, IReadOnlyList<string>> throughReferences = new(StringComparer.OrdinalIgnoreCase);

				foreach (var node in nodes.Values.Where(node => node.IsProject))
				{
					throughReferences[node.Id] = [.. Closure([node.Id], nodes, includeProjects: true)];
				}

				result.Add(
					new PackageTarget(
						target.Name,
						[.. direct.Order(StringComparer.OrdinalIgnoreCase)],
						[.. owned.Order(StringComparer.OrdinalIgnoreCase)],
						[.. resolved.Order(StringComparer.OrdinalIgnoreCase)],
						throughReferences
					)
				);
			}

			return result;
		}
		catch (JsonException)
		{
			// A partially written or older assets file: fall back to the declared references.
			return [];
		}
		catch (IOException)
		{
			return [];
		}
	}

	/// <summary>Reads the packages the project declares directly in every framework.</summary>
	/// <param name="root">The assets document.</param>
	/// <returns>The direct package identifiers.</returns>
	static HashSet<string> ReadDirectPackages(JsonElement root)
	{
		HashSet<string> packages = new(StringComparer.OrdinalIgnoreCase);

		if (
			!root.TryGetProperty("project", out var project)
			|| project.ValueKind != JsonValueKind.Object
			|| !project.TryGetProperty("frameworks", out var frameworks)
			|| frameworks.ValueKind != JsonValueKind.Object
		)
		{
			return packages;
		}

		foreach (var framework in frameworks.EnumerateObject())
		{
			if (
				!framework.Value.TryGetProperty("dependencies", out var dependencies)
				|| dependencies.ValueKind != JsonValueKind.Object
			)
			{
				continue;
			}

			foreach (var dependency in dependencies.EnumerateObject())
			{
				// A project reference is not a package: it explains packages rather than owning them.
				if (
					dependency.Value.ValueKind == JsonValueKind.Object
					&& dependency.Value.TryGetProperty("target", out var target)
					&& target.ValueKind == JsonValueKind.String
					&& !string.Equals(target.GetString(), "package", StringComparison.OrdinalIgnoreCase)
				)
				{
					continue;
				}

				packages.Add(PackageId(dependency.Name));
			}
		}

		return packages;
	}
}
