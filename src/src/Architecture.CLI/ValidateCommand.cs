using System.Text.Json;
using Purview.Architecture.Testing;

namespace Purview.Architecture.CLI;

/// <summary>Validates a policy without needing a build.</summary>
static class ValidateCommand
{
	/// <summary>The command's usage text.</summary>
	public const string Usage = "Usage: architests validate [--repository <path>]";

	/// <summary>Runs the command.</summary>
	/// <param name="args">The arguments after the command name.</param>
	/// <param name="output">Where ordinary output is written.</param>
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

		if (!RepositoryLocator.TryResolve(options.Repository, error, out var root))
			return CommandLine.Findings;

		var policyPath = Path.Combine(root, ArchitecturePolicy.RelativePath);

		try
		{
			var schemaFailures = PolicySchemaValidator.Validate(policyPath);

			if (schemaFailures.Count > 0)
			{
				error.WriteLine($"The policy at '{policyPath}' does not match its schema:");
				foreach (var failure in schemaFailures)
					error.WriteLine($"  {failure}");

				return CommandLine.Findings;
			}

			var policy = ArchitecturePolicy.Load(root);
			ArchitecturePolicyEvaluator evaluator = new(policy, new Dictionary<string, ProjectNode>());
			var violations = evaluator.PolicyIntegrity();

			if (violations.Count > 0)
			{
				error.WriteLine($"The policy at '{policyPath}' is not internally consistent:");
				error.WriteLine(ArchitecturePolicyEvaluator.Report(violations));
				return CommandLine.Findings;
			}
		}
		catch (JsonException exception)
		{
			error.WriteLine($"The policy at '{policyPath}' is not valid JSON: {exception.Message}");
			return CommandLine.Findings;
		}
		catch (InvalidOperationException exception)
		{
			error.WriteLine(exception.Message);
			return CommandLine.Findings;
		}

		output.WriteLine($"Policy is valid: {policyPath}");
		return CommandLine.Success;
	}
}
