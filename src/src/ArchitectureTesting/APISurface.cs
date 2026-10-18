using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Purview.Architecture.Testing;

/// <summary>
/// Reads the types and members a compiled assembly declares, by metadata only.
/// </summary>
/// <remarks>
/// <para>
/// No assembly is loaded or executed: <see cref="MetadataReader"/> reads the definition tables
/// directly, exactly as <see cref="AssemblyGraph"/> reads the reference table. The source files are
/// not needed, so a rule can police the API a consumer actually receives rather than what the
/// project happens to contain.
/// </para>
/// <para>
/// A type's own file path is deliberately absent: metadata records the namespace, not the folder, so
/// the namespace rule compares the declared namespace with the project's <c>RootNamespace</c> and
/// makes no claim about folders.
/// </para>
/// </remarks>
public static partial class APISurface
{
	/// <summary>Finds the assemblies to read, using the same roots and globs as the dependency check.</summary>
	/// <param name="repositoryRoot">The repository root.</param>
	/// <param name="rules">The policy's assembly rules.</param>
	/// <returns>The assembly file paths.</returns>
	public static IReadOnlyList<string> Discover(string repositoryRoot, AssemblyRules rules) =>
		AssemblyGraph.Discover(repositoryRoot, rules);

	/// <summary>Reads every project assembly among the given paths.</summary>
	/// <param name="assemblyPaths">The candidate assembly file paths.</param>
	/// <param name="projects">The project graph, used to attribute an assembly to a project.</param>
	/// <returns>The assemblies, keyed by project name.</returns>
	public static IReadOnlyDictionary<string, APIAssembly> Load(
		IEnumerable<string> assemblyPaths,
		IReadOnlyDictionary<string, ProjectNode> projects
	)
	{
		ArgumentNullException.ThrowIfNull(assemblyPaths);
		ArgumentNullException.ThrowIfNull(projects);

		Dictionary<string, APIAssembly> assemblies = new(StringComparer.OrdinalIgnoreCase);

		foreach (var path in assemblyPaths)
		{
			var assembly = ReadAssembly(path, projects);

			if (assembly is null)
				continue;

			// A repository scan can find the same project in Debug, Release and publish output. The
			// newest build is the one the repository is working on, so it wins.
			if (
				assemblies.TryGetValue(assembly.ProjectName, out var existing)
				&& existing.Path.Length > 0
				&& File.GetLastWriteTimeUtc(existing.Path) >= File.GetLastWriteTimeUtc(assembly.Path)
			)
			{
				continue;
			}

			assemblies[assembly.ProjectName] = assembly;
		}

		return assemblies;
	}

	/// <summary>Reads one assembly's declared types.</summary>
	/// <param name="assemblyPath">The assembly file path.</param>
	/// <returns>The types, or an empty list when the file is not a managed assembly.</returns>
	public static IReadOnlyList<APIType> Read(string assemblyPath) => ReadAssembly(assemblyPath, null)?.Types ?? [];

	/// <summary>Reads one assembly and attributes it to a project.</summary>
	/// <param name="assemblyPath">The assembly file path.</param>
	/// <param name="projects">
	/// The project graph. When <see langword="null"/>, the assembly is read without attribution.
	/// </param>
	/// <returns>
	/// The assembly, or <see langword="null"/> when the file is not managed or is not a project.
	/// </returns>
	public static APIAssembly? ReadAssembly(string assemblyPath, IReadOnlyDictionary<string, ProjectNode>? projects)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

		try
		{
			using var stream = File.OpenRead(assemblyPath);
			using PEReader peReader = new(stream);

			// A native dependency in the output has no managed metadata.
			if (!peReader.HasMetadata)
				return null;

			var reader = peReader.GetMetadataReader();
			var assemblyName = reader.GetString(reader.GetAssemblyDefinition().Name);

			// Satellite resource assemblies declare no types worth policing.
			if (assemblyName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
				return null;

			var projectName = projects is null
				? string.Empty
				: AssemblyAttribution.Resolve(assemblyName, reader, projects) ?? string.Empty;

			// Not a project assembly: a package, and packages are outside the policy.
			if (projects is not null && projectName.Length == 0)
				return null;

			List<APIType> types = [];
			foreach (var handle in reader.TypeDefinitions)
			{
				try
				{
					var type = ReadType(reader, handle, projectName);

					if (type is not null)
						types.Add(type);
				}
				catch (BadImageFormatException exception)
				{
					throw new InvalidOperationException(
						$"Reading '{MetadataNames.Definition(reader, handle)}' failed.",
						exception
					);
				}
			}

			var attributes = ReadAttributes(reader, reader.GetAssemblyDefinition().GetCustomAttributes());

			return new APIAssembly(projectName, assemblyName, attributes, types) { Path = assemblyPath };
		}
		catch (BadImageFormatException)
		{
			// A non-PE file that happened to match the glob.
			return null;
		}
		catch (IOException)
		{
			// A file being written by another build, or otherwise unreadable this run.
			return null;
		}
	}
}
