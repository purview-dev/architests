namespace Purview.Architecture.CLI;

/// <summary>Dispatches the API baseline commands.</summary>
static class APICommand
{
	/// <summary>The command's usage text.</summary>
	public const string Usage = """
		Usage: architests api <capture|check> [options]

		  capture   Write the API baseline from the compiled assemblies.
		  check     Compare the compiled assemblies with the baseline.
		""";

	/// <summary>Runs the command.</summary>
	/// <param name="args">The arguments after <c>api</c>.</param>
	/// <param name="output">Where ordinary output is written.</param>
	/// <param name="error">Where findings and usage errors are written.</param>
	/// <returns>The exit code.</returns>
	public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
	{
		if (args.Count == 0)
		{
			error.WriteLine(Usage);
			return CommandLine.UsageError;
		}

		return args[0] switch
		{
			"capture" => APICaptureCommand.Run([.. args.Skip(1)], output, error),
			"check" => APICheckCommand.Run([.. args.Skip(1)], output, error),
			_ => Unknown(args[0], error),
		};
	}

	/// <summary>Reports an unknown API verb.</summary>
	/// <param name="verb">The verb that was asked for.</param>
	/// <param name="error">Where the message is written.</param>
	/// <returns>The usage exit code.</returns>
	static int Unknown(string verb, TextWriter error)
	{
		error.WriteLine($"Expected 'capture' or 'check', not '{verb}'.");
		error.WriteLine();
		error.WriteLine(Usage);
		return CommandLine.UsageError;
	}
}
