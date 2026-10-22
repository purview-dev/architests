using System.Reflection.Metadata;

namespace Purview.Architecture.Testing;

/// <summary>Attributes a compiled assembly to the project that built it.</summary>
/// <remarks>
/// Shared by the dependency reader and the API-surface reader, so both agree on which project an
/// assembly belongs to. A project that overrides its <c>AssemblyName</c> still maps through the
/// SDK's generated <c>&lt;ProjectName&gt;Assembly</c> marker, which is exact; a name match is the
/// fallback for a repository that opted the marker out.
/// </remarks>
static class AssemblyAttribution
{
	/// <summary>Attributes an assembly to a project by the SDK marker, then by exact name.</summary>
	/// <param name="assemblyName">The assembly's simple name.</param>
	/// <param name="reader">The assembly's metadata.</param>
	/// <param name="projects">The project graph.</param>
	/// <returns>The project name, or <see langword="null"/> when the assembly is not a project.</returns>
	public static string? Resolve(
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

	/// <summary>Strips everything that is not a word character, matching the SDK's marker naming.</summary>
	/// <param name="value">The value to strip.</param>
	/// <returns>The stripped value.</returns>
	static string RemoveNonWord(string value) => new([.. value.Where(char.IsLetterOrDigit)]);
}
