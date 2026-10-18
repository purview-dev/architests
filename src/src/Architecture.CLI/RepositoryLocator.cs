using Purview.Architecture.Testing;

namespace Purview.Architecture.CLI;

/// <summary>Resolves the repository root a command runs against.</summary>
static class RepositoryLocator
{
	/// <summary>Resolves the root and confirms a policy exists there.</summary>
	/// <param name="repository">
	/// The repository root, or <see langword="null"/> to probe upward from the working directory.
	/// </param>
	/// <param name="error">Where a resolution failure is written.</param>
	/// <param name="root">The resolved repository root.</param>
	/// <returns><see langword="true"/> when a root and its policy were found.</returns>
	public static bool TryResolve(string? repository, TextWriter error, out string root)
	{
		if (repository is not null)
		{
			root = Path.GetFullPath(repository);

			if (!Directory.Exists(root))
			{
				error.WriteLine($"The repository folder '{root}' does not exist.");
				return false;
			}
		}
		else
		{
			try
			{
				// A tool runs from the repository being checked, not from its own install folder.
				root = ArchitecturePolicy.RepositoryRoot(Directory.GetCurrentDirectory());
			}
			catch (InvalidOperationException exception)
			{
				root = string.Empty;
				error.WriteLine(exception.Message);
				return false;
			}
		}

		var policyPath = Path.Combine(root, ArchitecturePolicy.RelativePath);

		if (!File.Exists(policyPath))
		{
			error.WriteLine($"No architecture policy was found at '{policyPath}'. Add one, or pass --repository.");
			return false;
		}

		return true;
	}
}
