using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Purview.Architecture.Testing;

/// <summary>A type one assembly references, and the project that defines it.</summary>
/// <param name="TargetProject">
/// The project the referenced type belongs to, or empty when the type is not part of a project (a
/// package or a framework type). An empty value means the reference is outside the policy.
/// </param>
/// <param name="TypeName">The referenced type's namespace-qualified name.</param>
public sealed record AssemblyReference(string TargetProject, string TypeName);

/// <summary>A compiled assembly and the external types its metadata references.</summary>
/// <param name="ProjectName">The project the assembly was built from.</param>
/// <param name="AssemblyName">The assembly's simple name.</param>
/// <param name="References">Every external type the assembly references that belongs to a project.</param>
public sealed record AssemblyNode(string ProjectName, string AssemblyName, IReadOnlyList<AssemblyReference> References);

/// <summary>
/// Reads compiled assemblies by metadata only, so a type-level dependency can be checked.
/// </summary>
/// <remarks>
/// <para>
/// No assembly is loaded or executed: <see cref="MetadataReader"/> reads the metadata tables directly,
/// which is why this needs no probing path and cannot run a type initialiser. The
/// <c>TypeReference</c> table is the whole dependency set - the compiler emits a row for every
/// external type a member signature, base type, interface, generic argument, attribute or method body
/// touches, including a dependency that only arrives transitively.
/// </para>
/// <para>
/// An assembly is attributed to a project by the SDK's generated <c>&lt;ProjectName&gt;Assembly</c>
/// marker class in the global namespace, which is exact even when a project overrides
/// <c>AssemblyName</c>, or by an exact project-name match when a repository opts the marker out. A
/// reference is resolved against the assemblies that were actually scanned, never by name guessing:
/// a package assembly such as <c>Microsoft.Extensions.Logging.Abstractions</c> is not
/// <c>Abstractions</c>, and a package is not governed by the policy.
/// </para>
/// </remarks>
public static class AssemblyGraph
{
	/// <summary>Finds the assemblies to read.</summary>
	/// <param name="repositoryRoot">The repository root, used to resolve relative assembly roots.</param>
	/// <param name="rules">The policy's assembly rules.</param>
	/// <returns>The assembly file paths.</returns>
	/// <remarks>
	/// An empty <see cref="AssemblyRules.AssemblyRoots"/> scans the test output directory, which
	/// already holds every project the test project references, directly or transitively. Intermediate
	/// (<c>obj</c>) output is skipped so reference assemblies never stand in for the real ones.
	/// </remarks>
	public static IReadOnlyList<string> Discover(string repositoryRoot, AssemblyRules rules)
	{
		ArgumentNullException.ThrowIfNull(rules);

		var roots =
			rules.AssemblyRoots.Count == 0
				? [AppContext.BaseDirectory]
				: rules.AssemblyRoots.Select(root => Path.Combine(repositoryRoot, root));

		var globs = rules.AssemblyGlobs.Count == 0 ? ["*.dll"] : rules.AssemblyGlobs;

		List<string> paths = [];
		foreach (var root in roots)
		{
			if (!Directory.Exists(root))
				continue;

			foreach (var glob in globs)
			{
				paths.AddRange(
					Directory
						.EnumerateFiles(root, glob, SearchOption.AllDirectories)
						.Where(path => !IsIntermediate(path))
				);
			}
		}

		return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
	}

	/// <summary>Reads the assemblies and resolves each to its project.</summary>
	/// <param name="assemblyPaths">The assembly file paths.</param>
	/// <param name="projects">The project graph, used to attribute an assembly to a project.</param>
	/// <returns>The assemblies, keyed by project name.</returns>
	public static IReadOnlyDictionary<string, AssemblyNode> Load(
		IEnumerable<string> assemblyPaths,
		IReadOnlyDictionary<string, ProjectNode> projects
	)
	{
		ArgumentNullException.ThrowIfNull(assemblyPaths);
		ArgumentNullException.ThrowIfNull(projects);

		// Pass one: identify each assembly and attribute it to a project. Only an assembly that is
		// itself a project can be a source, and only these names can be a reference target.
		List<ResolvedAssembly> resolved = [];
		foreach (var path in assemblyPaths)
		{
			var identity = ReadIdentity(path, projects);
			if (identity is not null)
				resolved.Add(identity);
		}

		Dictionary<string, string> projectByAssembly = new(StringComparer.OrdinalIgnoreCase);
		foreach (var assembly in resolved)
			projectByAssembly[assembly.AssemblyName] = assembly.ProjectName;

		// Pass two: read each source's references against the resolved set.
		Dictionary<string, AssemblyNode> assemblies = new(StringComparer.OrdinalIgnoreCase);
		foreach (var assembly in resolved)
		{
			var references = ReadReferences(assembly.Path, assembly.AssemblyName, projectByAssembly);
			assemblies[assembly.ProjectName] = new AssemblyNode(
				assembly.ProjectName,
				assembly.AssemblyName,
				references
			);
		}

		return assemblies;
	}

	/// <summary>An assembly and the project it was built from.</summary>
	/// <param name="Path">The assembly file path.</param>
	/// <param name="AssemblyName">The assembly's simple name.</param>
	/// <param name="ProjectName">The project it belongs to.</param>
	sealed record ResolvedAssembly(string Path, string AssemblyName, string ProjectName);

	/// <summary>Reads one assembly's identity and resolves it to a project.</summary>
	/// <param name="path">The assembly file path.</param>
	/// <param name="projects">The project graph.</param>
	/// <returns>The resolved assembly, or <see langword="null"/> when it is not a project assembly.</returns>
	static ResolvedAssembly? ReadIdentity(string path, IReadOnlyDictionary<string, ProjectNode> projects)
	{
		try
		{
			using var stream = File.OpenRead(path);
			using PEReader peReader = new(stream);

			// A native dependency in the output has no managed metadata.
			if (!peReader.HasMetadata)
				return null;

			var reader = peReader.GetMetadataReader();
			var assemblyName = reader.GetString(reader.GetAssemblyDefinition().Name);

			// Satellite resource assemblies carry no type references.
			if (assemblyName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
				return null;

			var project = ResolveProject(assemblyName, reader, projects);

			return project is null ? null : new ResolvedAssembly(path, assemblyName, project);
		}
		catch (BadImageFormatException)
		{
			// A non-PE file that happened to match the glob.
			return null;
		}
	}

	/// <summary>Reads the project-owned types one assembly references.</summary>
	/// <param name="path">The assembly file path.</param>
	/// <param name="assemblyName">The assembly's own simple name, so self-references are skipped.</param>
	/// <param name="projectByAssembly">The resolved assembly-to-project map.</param>
	/// <returns>The references.</returns>
	static List<AssemblyReference> ReadReferences(
		string path,
		string assemblyName,
		Dictionary<string, string> projectByAssembly
	)
	{
		try
		{
			using var stream = File.OpenRead(path);
			using PEReader peReader = new(stream);
			var reader = peReader.GetMetadataReader();

			List<AssemblyReference> references = [];
			foreach (var handle in reader.TypeReferences)
			{
				var typeReference = reader.GetTypeReference(handle);
				var scope = ResolveScopeAssembly(reader, typeReference.ResolutionScope);

				// A same-assembly reference is a TypeDefinition, not a TypeReference, so this is a
				// guard rather than an expected case. An unresolved scope means a package.
				if (scope is null || string.Equals(scope, assemblyName, StringComparison.OrdinalIgnoreCase))
					continue;

				if (!projectByAssembly.TryGetValue(scope, out var targetProject))
					continue;

				var typeName = typeReference.Namespace.IsNil
					? reader.GetString(typeReference.Name)
					: $"{reader.GetString(typeReference.Namespace)}.{reader.GetString(typeReference.Name)}";

				references.Add(new AssemblyReference(targetProject, typeName));
			}

			return references;
		}
		catch (BadImageFormatException)
		{
			return [];
		}
	}

	/// <summary>Attributes an assembly to a project by the SDK marker, then by exact name.</summary>
	/// <param name="assemblyName">The assembly's simple name.</param>
	/// <param name="reader">The assembly's metadata.</param>
	/// <param name="projects">The project graph.</param>
	/// <returns>The project name, or <see langword="null"/>.</returns>
	static string? ResolveProject(
		string assemblyName,
		MetadataReader reader,
		IReadOnlyDictionary<string, ProjectNode> projects
	)
	{
		// The SDK writes a global marker class named after the project for every production project,
		// so an assembly whose name is unrelated to its project (an explicit AssemblyName) still maps.
		foreach (var handle in reader.TypeDefinitions)
		{
			var type = reader.GetTypeDefinition(handle);
			if (!type.Namespace.IsNil)
				continue;

			var typeName = reader.GetString(type.Name);
			if (!typeName.EndsWith("Assembly", StringComparison.Ordinal))
				continue;

			var marker = typeName[..^"Assembly".Length];
			var match = projects.Keys.FirstOrDefault(project =>
				string.Equals(RemoveNonWord(project), marker, StringComparison.OrdinalIgnoreCase)
			);

			if (match is not null)
				return match;
		}

		// A repository that opts the marker out, or a project the SDK did not build, still maps when
		// its assembly name is its project name.
		return projects.Keys.FirstOrDefault(project =>
			string.Equals(project, assemblyName, StringComparison.OrdinalIgnoreCase)
		);
	}

	/// <summary>Resolves a type reference's resolution scope to the assembly that defines it.</summary>
	/// <param name="reader">The assembly's metadata.</param>
	/// <param name="scope">The scope handle, which may be a nested type reference.</param>
	/// <returns>The defining assembly's simple name, or <see langword="null"/> for the same module.</returns>
	static string? ResolveScopeAssembly(MetadataReader reader, EntityHandle scope)
	{
		while (true)
		{
			if (scope.Kind == HandleKind.AssemblyReference)
				return reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name);

			if (scope.Kind == HandleKind.TypeReference)
			{
				// A nested type's scope is its declaring type reference.
				scope = reader.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
				continue;
			}

			return null;
		}
	}

	/// <summary>Reports whether a path is intermediate build output.</summary>
	/// <param name="path">The path to test.</param>
	/// <returns><see langword="true"/> when the path is under an <c>obj</c> folder.</returns>
	static bool IsIntermediate(string path) =>
		path.Replace('\\', '/').Contains("/obj/", StringComparison.OrdinalIgnoreCase);

	/// <summary>Strips everything that is not a word character, matching the SDK's marker naming.</summary>
	/// <param name="value">The value to strip.</param>
	/// <returns>The stripped value.</returns>
	static string RemoveNonWord(string value) => new([.. value.Where(char.IsLetterOrDigit)]);
}
