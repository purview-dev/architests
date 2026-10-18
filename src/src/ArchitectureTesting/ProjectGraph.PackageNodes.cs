using System.Text.Json;

namespace Purview.Architecture.Testing;

/// <summary>Reads and traverses one target's package nodes.</summary>
public static partial class ProjectGraph
{
	/// <summary>Reads one target's nodes and their dependency edges.</summary>
	/// <param name="target">The target element.</param>
	/// <returns>The nodes, keyed by package or project id.</returns>
	static Dictionary<string, PackageNode> ReadNodes(JsonElement target)
	{
		Dictionary<string, PackageNode> nodes = new(StringComparer.OrdinalIgnoreCase);

		foreach (var entry in target.EnumerateObject())
		{
			if (entry.Value.ValueKind != JsonValueKind.Object)
				continue;

			var type =
				entry.Value.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
					? typeElement.GetString() ?? string.Empty
					: string.Empty;

			List<string> dependencies = [];

			if (
				entry.Value.TryGetProperty("dependencies", out var dependencyElement)
				&& dependencyElement.ValueKind == JsonValueKind.Object
			)
			{
				dependencies.AddRange(
					dependencyElement.EnumerateObject().Select(dependency => PackageId(dependency.Name))
				);
			}

			var id = PackageId(entry.Name);
			nodes[id] = new PackageNode(id, type, dependencies);
		}

		return nodes;
	}

	/// <summary>Collects the packages reachable from a set of starting nodes.</summary>
	/// <param name="starts">The starting package or project ids.</param>
	/// <param name="nodes">The target's nodes.</param>
	/// <param name="includeProjects">Whether traversal may pass through project nodes.</param>
	/// <returns>The package identifiers.</returns>
	static HashSet<string> Closure(
		IEnumerable<string> starts,
		Dictionary<string, PackageNode> nodes,
		bool includeProjects
	)
	{
		HashSet<string> packages = new(StringComparer.OrdinalIgnoreCase);
		HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
		Queue<string> queue = new();

		foreach (var start in starts)
		{
			if (visited.Add(start))
				queue.Enqueue(start);
		}

		while (queue.Count > 0)
		{
			if (!nodes.TryGetValue(queue.Dequeue(), out var node))
				continue;

			if (node.IsPackage)
				packages.Add(node.Id);
			else if (!node.IsProject || !includeProjects)
				continue;

			foreach (var dependency in node.Dependencies)
			{
				if (visited.Add(dependency))
					queue.Enqueue(dependency);
			}
		}

		return packages;
	}

	/// <summary>Removes the version from an assets key such as <c>Package/1.0.0</c>.</summary>
	/// <param name="key">The assets key.</param>
	/// <returns>The package or project id.</returns>
	static string PackageId(string key)
	{
		var separator = key.IndexOf('/', StringComparison.Ordinal);
		return separator < 0 ? key : key[..separator];
	}

	/// <summary>One node in a target's restore graph.</summary>
	/// <param name="Id">The package or project id.</param>
	/// <param name="Type">The assets type: <c>package</c> or <c>project</c>.</param>
	/// <param name="Dependencies">The ids this node depends on.</param>
	sealed record PackageNode(string Id, string Type, IReadOnlyList<string> Dependencies)
	{
		/// <summary>
		/// Whether the node is a package. An assets node with no <c>type</c> is a package, which is
		/// how older restore output recorded it.
		/// </summary>
		public bool IsPackage =>
			string.IsNullOrEmpty(Type) || string.Equals(Type, "package", StringComparison.OrdinalIgnoreCase);

		/// <summary>Whether the node is a project reference.</summary>
		public bool IsProject => string.Equals(Type, "project", StringComparison.OrdinalIgnoreCase);
	}
}
