using System.Globalization;
using System.Text;
using Purview.Architecture.Testing;

namespace Purview.Architecture.CLI;

/// <summary>Renders the project graph.</summary>
static class GraphCommand
{
	/// <summary>The command's usage text.</summary>
	public const string Usage = "Usage: architests graph [--format text|mermaid] [--repository <path>]";

	/// <summary>Runs the command.</summary>
	/// <param name="args">The arguments after <c>graph</c>.</param>
	/// <param name="output">Where the graph is written.</param>
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

		if (options.Format is not ("text" or "mermaid"))
		{
			error.WriteLine($"Unknown format '{options.Format}'. Use 'text' or 'mermaid'.");
			error.WriteLine();
			error.WriteLine(Usage);
			return CommandLine.UsageError;
		}

		if (!RepositoryLocator.TryResolve(options.Repository, error, out var root))
			return CommandLine.Findings;

		try
		{
			var policy = ArchitecturePolicy.Load(root);
			var projects = ProjectGraph.Load(root, policy.SourceRoots);

			if (projects.Count == 0)
			{
				error.WriteLine("No projects were found. Check sourceRoots in the policy, or build the repository.");
				return CommandLine.Findings;
			}

			output.Write(options.Format == "mermaid" ? RenderMermaid(projects, policy) : RenderText(projects, policy));

			return CommandLine.Success;
		}
		catch (System.Text.Json.JsonException exception)
		{
			error.WriteLine($"The architecture policy is not valid JSON: {exception.Message}");
			return CommandLine.Findings;
		}
		catch (InvalidOperationException exception)
		{
			error.WriteLine(exception.Message);
			return CommandLine.Findings;
		}
	}

	/// <summary>Renders the graph as an indented text tree.</summary>
	/// <param name="projects">The project graph.</param>
	/// <param name="policy">The policy.</param>
	/// <returns>The rendering.</returns>
	static string RenderText(IReadOnlyDictionary<string, ProjectNode> projects, ArchitecturePolicy policy)
	{
		StringBuilder builder = new();

		foreach (var project in projects.Values.OrderBy(project => project.Name, StringComparer.Ordinal))
		{
			var responsibility = policy.Classify(project.Name);

			var heading = responsibility is null
				? project.Name
				: $"{project.Name} ({responsibility.Name}, {responsibility.Category})";

			builder.AppendLine(heading);

			foreach (var reference in project.ProjectReferences.Order(StringComparer.Ordinal))
				builder.AppendLine(CultureInfo.InvariantCulture, $"  -> {reference}");
		}

		return builder.ToString();
	}

	/// <summary>Renders the graph as a Mermaid flowchart.</summary>
	/// <param name="projects">The project graph.</param>
	/// <param name="policy">The policy.</param>
	/// <returns>The rendering.</returns>
	/// <remarks>Mermaid node identifiers cannot contain dots, so they are replaced with underscores.</remarks>
	static string RenderMermaid(IReadOnlyDictionary<string, ProjectNode> projects, ArchitecturePolicy policy)
	{
		StringBuilder builder = new();
		builder.AppendLine("graph LR");

		foreach (var project in projects.Values.OrderBy(project => project.Name, StringComparer.Ordinal))
		{
			var responsibility = policy.Classify(project.Name);
			var label = responsibility is null
				? project.Name
				: $"{project.Name}<br/>{responsibility.Name} · {responsibility.Category}";

			builder.AppendLine(CultureInfo.InvariantCulture, $"    {Id(project.Name)}[\"{label}\"]");
		}

		foreach (var project in projects.Values.OrderBy(project => project.Name, StringComparer.Ordinal))
		{
			foreach (var reference in project.ProjectReferences.Order(StringComparer.Ordinal))
			{
				if (projects.ContainsKey(reference))
					builder.AppendLine(CultureInfo.InvariantCulture, $"    {Id(project.Name)} --> {Id(reference)}");
			}
		}

		return builder.ToString();
	}

	/// <summary>Makes a Mermaid-safe node identifier.</summary>
	/// <param name="name">The project name.</param>
	/// <returns>The identifier.</returns>
	static string Id(string name) =>
		new([.. name.Select(character => char.IsLetterOrDigit(character) ? character : '_')]);
}
