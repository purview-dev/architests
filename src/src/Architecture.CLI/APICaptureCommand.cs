using Purview.Architecture.Testing;

namespace Purview.Architecture.CLI;

/// <summary>Writes the API baseline from the compiled assemblies.</summary>
static class APICaptureCommand
{
	/// <summary>The command's usage text.</summary>
	public const string Usage = "Usage: architests api capture [--repository <path>] [--baseline <path>]";

	/// <summary>Runs the command.</summary>
	/// <param name="args">The arguments after <c>api capture</c>.</param>
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

		var context = APIContext.TryLoad(options.Repository, error);

		if (context is null)
			return CommandLine.Findings;

		var baseline = APIBaseline.Capture(context.Surfaces, context.Policy);
		var path = context.BaselinePath(options.Baseline);

		try
		{
			baseline.Write(path);
		}
		catch (IOException exception)
		{
			error.WriteLine($"The baseline could not be written to '{path}': {exception.Message}");
			return CommandLine.Findings;
		}
		catch (UnauthorizedAccessException exception)
		{
			error.WriteLine($"The baseline could not be written to '{path}': {exception.Message}");
			return CommandLine.Findings;
		}

		var types = baseline.Projects.Sum(project => project.Types.Count);

		output.WriteLine($"Captured {baseline.Projects.Count} project(s) and {types} public type(s) to {path}.");

		return CommandLine.Success;
	}
}
