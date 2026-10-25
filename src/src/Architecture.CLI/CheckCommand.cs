using System.Text.Json;
using Purview.Architecture.Testing;

namespace Purview.Architecture.CLI;

/// <summary>Runs the whole rule set against a repository.</summary>
/// <remarks>
/// <c>validate</c> checks the policy document; this checks the repository against it. Until now
/// the full rule set could only be run through TUnit, so a repository without a test project - or
/// a pipeline step that runs before the tests - had no way to ask the question at all.
/// </remarks>
static class CheckCommand
{
	/// <summary>The command's usage text.</summary>
	public const string Usage = "Usage: architests check [--format text|json|sarif] [--repository <path>]";

	static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	/// <summary>Runs the command.</summary>
	/// <param name="args">The arguments after <c>check</c>.</param>
	/// <param name="output">Where the report is written.</param>
	/// <param name="error">Where findings and usage errors are written.</param>
	/// <returns>The exit code.</returns>
	public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
	{
		if (!CommandOptionsParser.TryParse(args, allowStale: false, Usage, error, out var options, out var exitCode))
			return exitCode;

		if (options.Help)
		{
			output.WriteLine(Usage);
			return CommandLine.Success;
		}

		if (options.Format is not ("text" or "json" or "sarif"))
		{
			error.WriteLine($"Unknown format '{options.Format}'. Use 'text', 'json' or 'sarif'.");
			error.WriteLine();
			error.WriteLine(Usage);
			return CommandLine.UsageError;
		}

		if (!RepositoryLocator.TryResolve(options.Repository, error, out var root))
			return CommandLine.Findings;

		ArchitecturePolicyEvaluator evaluator;

		try
		{
			evaluator = ArchitecturePolicyEvaluator.ForRepository(root);
		}
		catch (FileNotFoundException exception)
		{
			error.WriteLine(exception.Message);
			return CommandLine.Findings;
		}
		catch (JsonException exception)
		{
			error.WriteLine($"The architecture policy is not valid JSON: {exception.Message}");
			return CommandLine.Findings;
		}
		catch (InvalidOperationException exception)
		{
			error.WriteLine(exception.Message);
			return CommandLine.Findings;
		}

		var violations = evaluator.EvaluateAll();

		// The machine-readable formats always produce a document, including an empty one. A clean
		// run that printed prose instead would hand the consuming step a file that is not JSON, and
		// an empty SARIF run is also what tells code scanning to clear findings that are now fixed -
		// without it, a violation someone has repaired stays on the dashboard forever.
		switch (options.Format)
		{
			case "json":
				output.WriteLine(JsonSerializer.Serialize(violations, JsonOptions));
				break;

			case "sarif":
				output.WriteLine(SarifReport.Render(violations, root, evaluator.Projects));
				break;

			default:
				if (violations.Count == 0)
					output.WriteLine("The repository respects its architecture policy.");

				foreach (var violation in violations)
					output.WriteLine($"{violation.Rule}: {violation.Message}");

				break;
		}

		if (violations.Count == 0)
			return CommandLine.Success;

		error.WriteLine($"{violations.Count} architecture violation(s). See the output above.");

		return CommandLine.Findings;
	}
}
