using System.Reflection;

namespace Purview.Architecture.CLI;

/// <summary>Parses the command line and dispatches to one command.</summary>
/// <remarks>
/// The command surface is small, so it is parsed directly rather than through a command-line
/// library: the tool keeps no dependency beyond the evaluator, and the parser is itself a unit
/// that can be tested. The exit codes are part of the contract, because a CI step reads them:
/// <see cref="Success"/> means the check passed, <see cref="Findings"/> means the policy was
/// broken, and <see cref="UsageError"/> means the command line was wrong.
/// </remarks>
public static class CommandLine
{
	/// <summary>The exit code for a clean run.</summary>
	public const int Success = 0;

	/// <summary>The exit code for a run that found policy violations.</summary>
	public const int Findings = 1;

	/// <summary>The exit code for a malformed command line.</summary>
	public const int UsageError = 2;

	const string Usage = """
		architests - run the architecture policy outside TUnit

		Usage:
		  architests <command> [options]

		Commands:
		  validate        Validate the policy against its schema and its own integrity rules.
		  api capture     Write the API baseline from the compiled assemblies.
		  api check       Compare the compiled assemblies with the baseline.
		  waivers list    List the policy's waivers, with their review dates.
		  graph           Render the project graph.

		Options:
		  -h, --help      Show this help.
		  --version       Show the tool version.
		""";

	/// <summary>The tool version, from the assembly's informational version.</summary>
	public static string Version { get; } = ReadVersion();

	/// <summary>Runs the tool.</summary>
	/// <param name="args">The command-line arguments, without the executable name.</param>
	/// <param name="output">Where ordinary output is written.</param>
	/// <param name="error">Where usage errors are written.</param>
	/// <returns>The process exit code.</returns>
	public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
	{
		ArgumentNullException.ThrowIfNull(args);
		ArgumentNullException.ThrowIfNull(output);
		ArgumentNullException.ThrowIfNull(error);

		if (args.Count == 0)
		{
			output.WriteLine(Usage);
			return Success;
		}

		switch (args[0])
		{
			case "-h":
			case "--help":
			case "help":
				output.WriteLine(Usage);
				return Success;

			case "--version":
			case "version":
				output.WriteLine(Version);
				return Success;

			case "validate":
				return ValidateCommand.Run([.. args.Skip(1)], output, error);

			case "api":
				return APICommand.Run([.. args.Skip(1)], output, error);

			case "waivers":
				return WaiversCommand.Run([.. args.Skip(1)], output, error);

			case "graph":
				return GraphCommand.Run([.. args.Skip(1)], output, error);

			default:
				return UnknownCommand(args[0], error);
		}
	}

	/// <summary>Reports a command the tool does not implement.</summary>
	/// <param name="command">The command that was asked for.</param>
	/// <param name="error">Where the message is written.</param>
	/// <returns><see cref="UsageError"/>.</returns>
	static int UnknownCommand(string command, TextWriter error)
	{
		error.WriteLine($"Unknown command '{command}'.");
		error.WriteLine();
		error.WriteLine(Usage);
		return UsageError;
	}

	/// <summary>Reads the informational version without its source-revision metadata.</summary>
	/// <returns>The version, or <c>unknown</c>.</returns>
	static string ReadVersion()
	{
		var assembly = typeof(CommandLine).Assembly;
		var version =
			assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
			?? assembly.GetName().Version?.ToString()
			?? "unknown";

		// SourceLink appends "+<commit>", which is noise in a version banner.
		var metadata = version.IndexOf('+', StringComparison.Ordinal);
		return metadata < 0 ? version : version[..metadata];
	}
}
