using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Purview.Architecture.Testing;

/// <summary>A committed snapshot of the public API, grouped by project and responsibility.</summary>
/// <param name="Version">The baseline document version.</param>
/// <param name="Projects">The projects and their public declarations.</param>
/// <remarks>
/// The baseline is the reviewable record of the API a consumer receives. It is written by
/// <c>architests api capture</c>, compared by <c>architests api check</c> and by the
/// <c>apiSurface</c> rule, and it is canonical: sorted, stable and free of timestamps, so an
/// unchanged API regenerates byte for byte.
/// </remarks>
public sealed record APIBaseline(int Version, IReadOnlyList<APIBaselineProject> Projects)
{
	/// <summary>The baseline document version this package writes.</summary>
	public const int CurrentVersion = 1;

	static readonly JsonSerializerOptions ReaderOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		ReadCommentHandling = JsonCommentHandling.Skip,
		AllowTrailingCommas = true,
	};

	static readonly JsonSerializerOptions WriterOptions = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
	};

	/// <summary>The baseline file's location relative to a repository root.</summary>
	public static string RelativePath { get; } = Path.Combine(".config", "api-surface.json");

	/// <summary>Captures the public API of every project assembly.</summary>
	/// <param name="assemblies">The API surfaces, keyed by project name.</param>
	/// <param name="policy">The policy, used to record each project's responsibility.</param>
	/// <returns>The baseline.</returns>
	public static APIBaseline Capture(IReadOnlyDictionary<string, APIAssembly> assemblies, ArchitecturePolicy policy)
	{
		ArgumentNullException.ThrowIfNull(assemblies);
		ArgumentNullException.ThrowIfNull(policy);

		return new APIBaseline(
			CurrentVersion,
			[
				.. assemblies
					.Values.Where(assembly => assembly.Types.Any(InSurface))
					.Select(assembly => new APIBaselineProject(
						assembly.ProjectName,
						policy.Classify(assembly.ProjectName)?.Name ?? string.Empty,
						[.. assembly.Types.Where(InSurface).Select(TypeEntry)]
					)),
			]
		).Sorted();
	}

	/// <summary>Reports whether a type belongs in the committed contract.</summary>
	/// <param name="type">The type.</param>
	/// <returns><see langword="true"/> when the type is public and written by an author.</returns>
	/// <remarks>
	/// A compiler-synthesized container is public in metadata but is not API: nobody can name it,
	/// and the hash in its name changes whenever the source around it changes. Recording it would
	/// make the baseline churn on edits that alter no contract at all.
	/// </remarks>
	static bool InSurface(APIType type) => type.IsPublic && !type.IsCompilerSynthesized;

	/// <summary>Reads a baseline file.</summary>
	/// <param name="path">The baseline path.</param>
	/// <returns>The baseline.</returns>
	/// <exception cref="FileNotFoundException">The file does not exist.</exception>
	public static APIBaseline Read(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		if (!File.Exists(path))
		{
			throw new FileNotFoundException(
				$"The API baseline was not found at '{path}'. Run 'architests api capture' to write it.",
				path
			);
		}

		// The baseline is canonical JSON, so we can use the default serializer to read it.
		return JsonSerializer.Deserialize<APIBaseline>(File.ReadAllText(path), ReaderOptions)
			?? throw new InvalidOperationException($"The API baseline at '{path}' deserialised to null.");
	}

	/// <summary>Writes the baseline as canonical JSON.</summary>
	/// <param name="path">The path to write.</param>
	public void Write(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		var directory = Path.GetDirectoryName(Path.GetFullPath(path));

		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);

		File.WriteAllText(path, ToCanonicalJson());
	}

	/// <summary>Renders the baseline as canonical JSON.</summary>
	/// <returns>
	/// Sorted, indented JSON with a trailing newline, using forward line endings so the file is
	/// identical on Windows, Linux and macOS.
	/// </returns>
	public string ToCanonicalJson() =>
		JsonSerializer.Serialize(Sorted(), WriterOptions).ReplaceLineEndings("\n") + "\n";

	/// <summary>Sorts every list so an unchanged API produces an identical document.</summary>
	/// <returns>The sorted baseline.</returns>
	public APIBaseline Sorted() =>
		new(
			Version,
			[
				.. Projects
					.OrderBy(project => project.Project, StringComparer.Ordinal)
					.Select(project =>
						project with
						{
							Types =
							[
								.. project
									.Types.OrderBy(type => type.Name, StringComparer.Ordinal)
									.Select(type =>
										type with
										{
											Interfaces =
											[
												.. type.Interfaces.OrderBy(value => value, StringComparer.Ordinal),
											],
											// Ordered by the same identity the comparer keys on, so two
											// overloads that differ only in arity or return type keep a
											// stable, distinguishable order.
											Members =
											[
												.. type.Members.OrderBy(
													member => member.Signature,
													StringComparer.Ordinal
												),
											],
										}
									),
							],
						}
					),
			]
		);

	/// <summary>Builds the baseline entry for a type.</summary>
	/// <param name="type">The type.</param>
	/// <returns>The entry.</returns>
	static APIBaselineType TypeEntry(APIType type) =>
		new(
			type.FullName,
			type.Kind.ToString(),
			type.IsAbstract,
			type.IsSealed,
			type.BaseType,
			[.. type.Interfaces],
			[
				.. type.Members.Select(member => new APIBaselineMember(
					member.Kind.ToString(),
					member.Name,
					member.TypeName,
					member.IsStatic,
					[.. member.Parameters]
				)
				{
					GenericArity = member.GenericArity,
				}),
			]
		);
}
