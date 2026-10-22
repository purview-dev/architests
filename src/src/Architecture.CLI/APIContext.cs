using System.Text.Json;
using Purview.Architecture.Testing;

namespace Purview.Architecture.CLI;

/// <summary>The policy, project graph and public surfaces an API command works against.</summary>
/// <param name="Root">The repository root.</param>
/// <param name="Policy">The repository's policy.</param>
/// <param name="Surfaces">The public surfaces, keyed by project name.</param>
sealed record APIContext(string Root, ArchitecturePolicy Policy, IReadOnlyDictionary<string, APIAssembly> Surfaces)
{
	/// <summary>Resolves the baseline path for a command.</summary>
	/// <param name="baseline">The <c>--baseline</c> override, or <see langword="null"/>.</param>
	/// <returns>The absolute baseline path.</returns>
	public string BaselinePath(string? baseline)
	{
		var relative =
			baseline
			?? (
				string.IsNullOrWhiteSpace(Policy.APIRules.Baseline)
					? APIBaseline.RelativePath
					: Policy.APIRules.Baseline
			);

		return Path.IsPathRooted(relative) ? relative : Path.Combine(Root, relative);
	}

	/// <summary>Loads a repository, its policy and its compiled surfaces.</summary>
	/// <param name="repository">The repository root, or <see langword="null"/> to probe upward.</param>
	/// <param name="error">Where a failure is written.</param>
	/// <returns>The context, or <see langword="null"/> when it could not be loaded.</returns>
	public static APIContext? TryLoad(string? repository, TextWriter error)
	{
		if (!RepositoryLocator.TryResolve(repository, error, out var root))
			return null;

		try
		{
			var policy = ArchitecturePolicy.Load(root);
			var projects = ProjectGraph.Load(root, policy.SourceRoots);

			// An empty assemblyRoots means "the test output" to the evaluator, which is right inside a
			// test run and wrong inside a tool. A command scans the repository's own build output.
			var rules =
				policy.AssemblyRules.AssemblyRoots.Count == 0
					? new AssemblyRules { AssemblyRoots = policy.SourceRoots.Count == 0 ? ["."] : policy.SourceRoots }
					: policy.AssemblyRules;

			var paths = AssemblyGraph.Discover(root, rules);
			var surfaces = APISurface.Load(paths, projects);

			if (surfaces.Count == 0)
			{
				error.WriteLine(
					"No project assemblies were found. Build the repository first, or check "
						+ "assemblyRules.assemblyRoots in the policy."
				);
				return null;
			}

			return new APIContext(root, policy, surfaces);
		}
		catch (JsonException exception)
		{
			error.WriteLine($"The architecture policy is not valid JSON: {exception.Message}");
			return null;
		}
		catch (InvalidOperationException exception)
		{
			error.WriteLine(exception.Message);
			return null;
		}
	}
}
