using System.Text.Json;
using Purview.Architecture.Testing;

namespace Purview.Architecture.CLI;

/// <summary>Compares the compiled assemblies with the committed baseline.</summary>
static class APICheckCommand
{
	/// <summary>The command's usage text.</summary>
	public const string Usage =
		"Usage: architests api check [--repository <path>] [--baseline <path>] [--format text|json|sarif]";

	static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
	};

	/// <summary>Runs the command.</summary>
	/// <param name="args">The arguments after <c>api check</c>.</param>
	/// <param name="output">Where the result is written.</param>
	/// <param name="error">Where findings and usage errors are written.</param>
	/// <returns>The exit code.</returns>
	public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
	{
		if (!CommandOptionsParser.TryParse(args, allowStale: false, Usage, error, out var options, out var exitCode))
		{
			return exitCode;
		}

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

		var context = APIContext.TryLoad(options.Repository, error);

		if (context is null)
			return CommandLine.Findings;

		var path = context.BaselinePath(options.Baseline);

		APIBaseline baseline;

		try
		{
			baseline = APIBaseline.Read(path);
		}
		catch (FileNotFoundException exception)
		{
			error.WriteLine(exception.Message);
			return CommandLine.Findings;
		}
		catch (JsonException exception)
		{
			error.WriteLine($"The API baseline at '{path}' is not valid JSON: {exception.Message}");
			return CommandLine.Findings;
		}
		catch (InvalidOperationException exception)
		{
			error.WriteLine(exception.Message);
			return CommandLine.Findings;
		}

		var current = APIBaseline.Capture(context.Surfaces, context.Policy);
		var changes = APIBaselineComparer.Compare(baseline, current);

		if (changes.Count == 0)
		{
			output.WriteLine("API matches the baseline.");
			return CommandLine.Success;
		}

		switch (options.Format)
		{
			case "json":
				output.WriteLine(JsonSerializer.Serialize(changes, JsonOptions));
				break;

			case "sarif":
				output.WriteLine(SarifReport.Render(changes, context.Root, path));
				break;

			default:
				foreach (var change in changes)
					output.WriteLine($"{change.Kind}: {change.Detail}");
				break;
		}

		error.WriteLine(
			$"{changes.Count} API difference(s) from '{path}'. Run 'architests api capture' after reviewing them."
		);

		return CommandLine.Findings;
	}
}
