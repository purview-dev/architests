using System.Text.Json;
using Purview.Architecture.Testing;

namespace Purview.Architecture.CLI;

/// <summary>Compares the compiled assemblies with the committed baseline.</summary>
static class APICheckCommand
{
	/// <summary>The command's usage text.</summary>
	public const string Usage =
		"Usage: architests api check [--repository <path>] [--baseline <path>] [--format text|json]";

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
				output.WriteLine(RenderSarif(changes, context.Root, path));
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

	/// <summary>Renders the differences as a SARIF 2.1.0 document.</summary>
	/// <param name="changes">The differences.</param>
	/// <param name="root">The repository root, used for the artifact URI.</param>
	/// <param name="baselinePath">The baseline path.</param>
	/// <returns>The SARIF document.</returns>
	static string RenderSarif(IReadOnlyList<APIBaselineChange> changes, string root, string baselinePath)
	{
		var uri = Path.GetRelativePath(root, baselinePath).Replace('\\', '/');

		var results = changes
			.Select(change => new Dictionary<string, object?>
			{
				["ruleId"] = ArchitectureRules.APISurface,
				["level"] = change.Kind == APIBaselineChangeKind.Removed ? "error" : "warning",
				["message"] = new Dictionary<string, object?> { ["text"] = change.Detail },
				["locations"] = new[]
				{
					new Dictionary<string, object?>
					{
						["physicalLocation"] = new Dictionary<string, object?>
						{
							["artifactLocation"] = new Dictionary<string, object?> { ["uri"] = uri },
						},
					},
				},
			})
			.ToArray();

		Dictionary<string, object?> document = new()
		{
			["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
			["version"] = "2.1.0",
			["runs"] = new[]
			{
				new Dictionary<string, object?>
				{
					["tool"] = new Dictionary<string, object?>
					{
						["driver"] = new Dictionary<string, object?>
						{
							["name"] = "architests",
							["version"] = CommandLine.Version,
							["informationUri"] = "https://github.com/purview-dev/architests",
						},
					},
					["results"] = results,
				},
			},
		};

		return JsonSerializer.Serialize(document, JsonOptions);
	}
}
