using System.Text.Json;
using Purview.Architecture.Testing;

namespace Purview.Architecture.CLI;

/// <summary>Lists a policy's waivers and their review state.</summary>
static class WaiversCommand
{
	/// <summary>The command's usage text.</summary>
	public const string Usage = "Usage: architests waivers list [--stale] [--repository <path>]";

	/// <summary>Runs the command.</summary>
	/// <param name="args">The arguments after <c>waivers</c>.</param>
	/// <param name="output">Where the listing is written.</param>
	/// <param name="error">Where findings and usage errors are written.</param>
	/// <returns>The exit code.</returns>
	public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
	{
		if (args.Count == 0 || args[0] != "list")
		{
			error.WriteLine("Expected 'list'.");
			error.WriteLine();
			error.WriteLine(Usage);
			return CommandLine.UsageError;
		}

		if (
			!CommandOptionsParser.TryParse(
				[.. args.Skip(1)],
				allowStale: true,
				Usage,
				error,
				out var options,
				out var exitCode
			)
		)
		{
			return exitCode;
		}

		if (options.Help)
		{
			output.WriteLine(Usage);
			return CommandLine.Success;
		}

		if (!RepositoryLocator.TryResolve(options.Repository, error, out var root))
			return CommandLine.Findings;

		var policyPath = Path.Combine(root, ArchitecturePolicy.RelativePath);

		ArchitecturePolicy policy;

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

			policy = ArchitecturePolicy.Load(root);
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

		var reviews = WaiverReviewer.Review(policy, DateOnly.FromDateTime(DateTime.UtcNow));

		if (reviews.Count == 0)
		{
			output.WriteLine("No waivers.");
			return CommandLine.Success;
		}

		var selected = options.Stale ? [.. reviews.Where(review => review.IsStale)] : reviews;

		if (selected.Count == 0)
		{
			output.WriteLine("No stale waivers.");
			return CommandLine.Success;
		}

		foreach (var review in selected)
		{
			output.WriteLine($"{review.Waiver.Project} [{string.Join(", ", review.Waiver.Rules)}]: {review.Reason}");
		}

		return options.Stale ? CommandLine.Findings : CommandLine.Success;
	}
}
