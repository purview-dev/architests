using System.Xml.Linq;

namespace Purview.Architecture.Testing;

/// <summary>A project in the solution, with the references it declares.</summary>
/// <param name="Name">The project name, without extension.</param>
/// <param name="Path">The absolute path to the project file.</param>
/// <param name="ProjectReferences">The names of the projects it references directly.</param>
/// <param name="PackageReferences">The package identifiers it references directly.</param>
/// <param name="IsTestProject">Whether the project is a test project.</param>
public sealed record ProjectNode(
	string Name,
	string Path,
	IReadOnlyList<string> ProjectReferences,
	IReadOnlyList<string> PackageReferences,
	bool IsTestProject
)
{
	/// <summary>The framework references it declares directly, such as <c>Microsoft.AspNetCore.App</c>.</summary>
	public IReadOnlyList<string> FrameworkReferences { get; init; } = [];

	/// <summary>The analyzer assemblies it references directly.</summary>
	public IReadOnlyList<string> AnalyzerReferences { get; init; } = [];

	/// <summary>
	/// The project's declared root namespace. Defaults to the project name, which is the SDK's
	/// fallback when a project does not override it.
	/// </summary>
	public string RootNamespace { get; init; } = string.Empty;

	/// <summary>
	/// The packages restore resolved for the project, including transitive dependencies and packages
	/// injected by a shared props file. Read from <c>obj/project.assets.json</c>; empty before a
	/// restore.
	/// </summary>
	public IReadOnlyList<string> ResolvedPackages { get; init; } = [];

	/// <summary>
	/// The restore graph per target framework, used to separate the packages the project chose from
	/// the packages a referenced project brought in. Empty before a restore.
	/// </summary>
	public IReadOnlyList<PackageTarget> PackageTargets { get; init; } = [];

	/// <summary>
	/// The assembly the project builds. The reader falls back to the project name; restore records
	/// project dependencies by assembly name, so this is what maps them back.
	/// </summary>
	public string AssemblyName { get; init; } = string.Empty;
}

/// <summary>
/// Reads the solution's projects and their declared references straight from the project files.
/// </summary>
/// <remarks>
/// Project files are read because a declared <c>ProjectReference</c> is the thing a reviewer adds and
/// the thing the policy governs. Compiled assembly references are read separately by
/// <see cref="AssemblyGraph"/> - they catch a dependency that arrived indirectly, which the project
/// file does not show.
/// </remarks>
public static partial class ProjectGraph
{
	/// <summary>Loads every project beneath the repository's default source folder.</summary>
	/// <param name="repositoryRoot">The repository root.</param>
	/// <returns>The projects, keyed by name.</returns>
	public static IReadOnlyDictionary<string, ProjectNode> Load(string repositoryRoot) => Load(repositoryRoot, ["src"]);

	/// <summary>Loads every project beneath the given source roots.</summary>
	/// <param name="repositoryRoot">The repository root.</param>
	/// <param name="sourceRoots">
	/// Repo-relative folders to scan. An empty collection scans the repository root, which suits a
	/// flat layout.
	/// </param>
	/// <returns>The projects, keyed by name.</returns>
	/// <remarks>
	/// A source root that does not exist is skipped rather than throwing, because a repository may
	/// legitimately configure roots it does not all use. The caller must notice an *empty* result:
	/// an empty graph satisfies every other rule, so it would otherwise pass silently. That is what
	/// <see cref="ArchitecturePolicyEvaluator.GraphDiscovery"/> is for.
	/// </remarks>
	public static IReadOnlyDictionary<string, ProjectNode> Load(
		string repositoryRoot,
		IReadOnlyList<string> sourceRoots
	)
	{
		ArgumentNullException.ThrowIfNull(sourceRoots);

		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase);

		var roots =
			sourceRoots.Count == 0 ? [repositoryRoot] : sourceRoots.Select(root => Path.Combine(repositoryRoot, root));

		foreach (var sourceRoot in roots)
		{
			if (Directory.Exists(sourceRoot))
				Scan(sourceRoot, projects);
		}

		return RemapProjectReferences(projects);
	}

	/// <summary>Maps asset project keys back to project names.</summary>
	/// <param name="projects">The project graph, keyed by project name.</param>
	/// <returns>The same graph, with package closures keyed by project name.</returns>
	/// <remarks>
	/// Restore records a project dependency under its <c>AssemblyName</c>, which may be unrelated to
	/// the project name. The ownership analysis compares it with the declared
	/// <c>ProjectReference</c> names, so the keys are translated once here.
	/// </remarks>
	static Dictionary<string, ProjectNode> RemapProjectReferences(Dictionary<string, ProjectNode> projects)
	{
		Dictionary<string, string> byAssembly = new(StringComparer.OrdinalIgnoreCase);

		foreach (var project in projects.Values)
		{
			var assemblyName = string.IsNullOrWhiteSpace(project.AssemblyName) ? project.Name : project.AssemblyName;
			byAssembly.TryAdd(assemblyName, project.Name);
		}

		foreach (var name in projects.Keys.ToArray())
		{
			var project = projects[name];

			if (project.PackageTargets.Count == 0)
				continue;

			projects[name] = project with
			{
				PackageTargets =
				[
					.. project.PackageTargets.Select(target =>
						target with
						{
							ThroughReferences = RemapReferences(target.ThroughReferences, byAssembly, projects.Keys),
						}
					),
				],
			};
		}

		return projects;
	}

	/// <summary>Translates asset project keys to project names.</summary>
	/// <param name="references">The closures keyed by assembly name.</param>
	/// <param name="byAssembly">The assembly-to-project map.</param>
	/// <param name="projectNames">The project names, for the suffix fallback.</param>
	/// <returns>The closures keyed by project name.</returns>
	/// <remarks>
	/// The SDK derives an assembly name from the namespace prefix, so a project named
	/// <c>Application</c> can compile to <c>Purview.Architecture.Application</c>. An exact declared
	/// <c>AssemblyName</c> wins; otherwise a unique dotted suffix identifies the project.
	/// </remarks>
	static Dictionary<string, IReadOnlyList<string>> RemapReferences(
		IReadOnlyDictionary<string, IReadOnlyList<string>> references,
		Dictionary<string, string> byAssembly,
		IEnumerable<string> projectNames
	)
	{
		var names = projectNames.ToArray();
		Dictionary<string, IReadOnlyList<string>> remapped = new(StringComparer.OrdinalIgnoreCase);

		foreach (var reference in references)
		{
			remapped[ResolveProjectName(reference.Key, byAssembly, names)] = reference.Value;
		}

		return remapped;
	}

	/// <summary>Resolves an asset project key to a project name.</summary>
	/// <param name="key">The assets key, which is an assembly name.</param>
	/// <param name="byAssembly">The assembly-to-project map.</param>
	/// <param name="projectNames">The project names.</param>
	/// <returns>The project name, or the key when no single project matches.</returns>
	static string ResolveProjectName(
		string key,
		Dictionary<string, string> byAssembly,
		IReadOnlyList<string> projectNames
	)
	{
		if (byAssembly.TryGetValue(key, out var exact))
			return exact;

		var matches = projectNames
			.Where(name => key.EndsWith("." + name, StringComparison.OrdinalIgnoreCase))
			.ToArray();

		return matches.Length == 1 ? matches[0] : key;
	}

	/// <summary>Scans one source root for projects.</summary>
	/// <param name="sourceRoot">The folder to scan.</param>
	/// <param name="projects">The accumulating project map.</param>
	static void Scan(string sourceRoot, Dictionary<string, ProjectNode> projects)
	{
		foreach (var projectPath in Directory.EnumerateFiles(sourceRoot, "*.csproj", SearchOption.AllDirectories))
		{
			// bin/ and obj/ can hold copies of project files during a build; they are not the source.
			if (
				projectPath.Contains(
					$"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
					StringComparison.Ordinal
				)
				|| projectPath.Contains(
					$"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
					StringComparison.Ordinal
				)
			)
			{
				continue;
			}

			var node = Read(projectPath);
			projects[node.Name] = node;
		}
	}

	/// <summary>Reads a single project file.</summary>
	/// <param name="projectPath">The project file path.</param>
	/// <returns>The project node.</returns>
	static ProjectNode Read(string projectPath)
	{
		var document = XDocument.Load(projectPath);
		var name = Path.GetFileNameWithoutExtension(projectPath);

		var projectReferences = document
			.Descendants("ProjectReference")
			.Select(element => (string?)element.Attribute("Include"))
			.Where(include => !string.IsNullOrWhiteSpace(include))
			.Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', Path.DirectorySeparatorChar)))
			.Where(reference => !string.IsNullOrWhiteSpace(reference))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		var packageReferences = document
			.Descendants("PackageReference")
			.Select(element => (string?)element.Attribute("Include") ?? (string?)element.Attribute("Update"))
			.Where(include => !string.IsNullOrWhiteSpace(include))
			.Select(include => include!)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		var frameworkReferences = document
			.Descendants("FrameworkReference")
			.Select(element => (string?)element.Attribute("Include"))
			.Where(include => !string.IsNullOrWhiteSpace(include))
			.Select(include => include!)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		var analyzerReferences = document
			.Descendants("Analyzer")
			.Select(element => (string?)element.Attribute("Include"))
			.Where(include => !string.IsNullOrWhiteSpace(include))
			.Select(include => include!)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToArray();

		var rootNamespace = document.Descendants("RootNamespace").LastOrDefault()?.Value.Trim();
		var assemblyName = document.Descendants("AssemblyName").LastOrDefault()?.Value.Trim();
		var packageTargets = ReadPackageTargets(projectPath);

		// The SDK derives IsTestProject from the folder, so the path is the reliable signal here.
		var isTestProject =
			name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase)
			|| projectPath.Contains(
				$"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}",
				StringComparison.OrdinalIgnoreCase
			);

		return new ProjectNode(name, projectPath, projectReferences, packageReferences, isTestProject)
		{
			FrameworkReferences = frameworkReferences,
			AnalyzerReferences = analyzerReferences,
			RootNamespace = string.IsNullOrWhiteSpace(rootNamespace) ? name : rootNamespace,
			AssemblyName = string.IsNullOrWhiteSpace(assemblyName) ? name : assemblyName,
			ResolvedPackages =
			[
				.. packageTargets
					.SelectMany(target => target.Resolved)
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.Order(StringComparer.OrdinalIgnoreCase),
			],
			PackageTargets = packageTargets,
		};
	}

	/// <summary>Finds every dependency cycle in the project graph.</summary>
	/// <param name="projects">The projects to inspect.</param>
	/// <returns>Each cycle, rendered as the path that closes it.</returns>
	/// <remarks>
	/// MSBuild itself rejects a cycle, so this is a guard that reports the cycle readably rather than
	/// as a build error naming two projects. It also covers a graph assembled in a test fixture.
	/// </remarks>
	public static IReadOnlyList<string> FindCycles(IReadOnlyDictionary<string, ProjectNode> projects)
	{
		ArgumentNullException.ThrowIfNull(projects);

		List<string> cycles = [];
		Dictionary<string, int> state = [];

		foreach (var project in projects.Keys)
			Visit(project, []);

		return cycles;

		void Visit(string current, List<string> path)
		{
			// 1 = on the current path, 2 = fully explored and clean.
			if (state.TryGetValue(current, out var visited))
			{
				if (visited == 1)
				{
					var start = path.FindIndex(entry =>
						string.Equals(entry, current, StringComparison.OrdinalIgnoreCase)
					);
					if (start >= 0)
						cycles.Add(string.Join(" -> ", path[start..].Append(current)));
				}

				return;
			}

			state[current] = 1;
			path.Add(current);

			if (projects.TryGetValue(current, out var node))
			{
				foreach (var reference in node.ProjectReferences)
					Visit(reference, path);
			}

			path.RemoveAt(path.Count - 1);
			state[current] = 2;
		}
	}
}
