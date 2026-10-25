namespace Purview.Architecture.Testing.AcceptanceTests;

/// <summary>A repository on disk, its policy file, and what happened when the policy was read.</summary>
/// <remarks>
/// These scenarios are about the policy document itself rather than any one rule, so they need a
/// real directory: the behaviour under test is how a path, a glob or an unknown key is treated
/// when the policy is loaded.
/// </remarks>
public sealed class PolicyDocumentContext : IDisposable
{
	readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("PurviewPolicyDocument");

	/// <summary>The repository root the policy lives in.</summary>
	public string Root => _directory.FullName;

	/// <summary>The failure the policy produced, or <see langword="null"/> when it loaded.</summary>
	public Exception? Failure { get; set; }

	/// <summary>The policy, once it has been read.</summary>
	public ArchitecturePolicy? Policy { get; set; }

	/// <summary>The projects discovered, once the graph has been loaded.</summary>
	public IReadOnlyDictionary<string, ProjectNode>? Projects { get; set; }

	/// <summary>The violations reported, once every rule has been run.</summary>
	public IReadOnlyList<ArchitectureViolation>? Violations { get; set; }

	/// <summary>Writes the policy file.</summary>
	/// <param name="json">The policy document.</param>
	public void WritePolicy(string json)
	{
		var config = Path.Combine(Root, ".config");
		Directory.CreateDirectory(config);
		File.WriteAllText(Path.Combine(config, "architecture-policy.json"), json);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		try
		{
			_directory.Delete(recursive: true);
		}
		catch (IOException)
		{
			// A scenario that leaves a handle open must not fail the run over a temp folder.
		}
	}
}
