namespace Purview.Architecture.CLI;

/// <summary>The process entry point.</summary>
static class Program
{
	/// <summary>Runs the command line.</summary>
	/// <param name="args">The arguments, without the executable name.</param>
	/// <returns>The process exit code.</returns>
	static int Main(string[] args) => CommandLine.Run(args, Console.Out, Console.Error);
}
