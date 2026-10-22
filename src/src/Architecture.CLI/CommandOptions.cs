namespace Purview.Architecture.CLI;

/// <summary>The options a command accepts.</summary>
/// <param name="Repository">The repository root, or <see langword="null"/> to probe upward.</param>
/// <param name="Stale">Whether <c>waivers list</c> should show only stale waivers.</param>
/// <param name="Help">Whether the command was asked for its own help.</param>
/// <param name="Baseline">The API baseline path, or <see langword="null"/> for the policy's path.</param>
/// <param name="Format">The output format: <c>text</c> or <c>json</c>.</param>
sealed record CommandOptions(string? Repository, bool Stale, bool Help, string? Baseline, string Format);

/// <summary>Parses the options shared by the tool's commands.</summary>
/// <remarks>
/// The surface is deliberately tiny, so a hand-written parser keeps the tool dependency-free and
/// gives exact control of the exit code. Unknown input is a usage error rather than being ignored:
/// a typo in CI must not silently check the wrong thing.
/// </remarks>
static class CommandOptionsParser
{
	/// <summary>Parses a command's arguments.</summary>
	/// <param name="args">The arguments after the command name.</param>
	/// <param name="allowStale">Whether the command accepts <c>--stale</c>.</param>
	/// <param name="usage">The command's usage text, printed on a usage error.</param>
	/// <param name="error">Where a usage error is written.</param>
	/// <param name="options">The parsed options, when parsing succeeds.</param>
	/// <param name="exitCode">The exit code to return when parsing fails.</param>
	/// <returns><see langword="true"/> when the arguments were understood.</returns>
	public static bool TryParse(
		IReadOnlyList<string> args,
		bool allowStale,
		string usage,
		TextWriter error,
		out CommandOptions options,
		out int exitCode
	)
	{
		string? repository = null;
		string? baseline = null;
		var format = "text";
		var stale = false;
		var help = false;

		for (var index = 0; index < args.Count; index++)
		{
			var argument = args[index];

			if (argument is "-h" or "--help")
			{
				help = true;
				continue;
			}

			if (argument == "--stale" && allowStale)
			{
				stale = true;
				continue;
			}

			if (
				TryRead(argument, args, ref index, "--repository", out var value)
				|| TryRead(argument, args, ref index, "-r", out value)
			)
			{
				if (string.IsNullOrWhiteSpace(value))
				{
					exitCode = Fail("--repository needs a repository path.", usage, error);
					options = new CommandOptions(null, false, false, null, "text");
					return false;
				}

				repository = value;
				continue;
			}

			if (TryRead(argument, args, ref index, "--baseline", out value))
			{
				if (string.IsNullOrWhiteSpace(value))
				{
					exitCode = Fail("--baseline needs a path.", usage, error);
					options = new CommandOptions(null, false, false, null, "text");
					return false;
				}

				baseline = value;
				continue;
			}

			if (TryRead(argument, args, ref index, "--format", out value))
			{
				if (string.IsNullOrWhiteSpace(value))
				{
					exitCode = Fail("--format needs a value.", usage, error);
					options = new CommandOptions(null, false, false, null, "text");
					return false;
				}

				format = value;
				continue;
			}

			exitCode = Fail($"Unknown option '{argument}'.", usage, error);
			options = new CommandOptions(null, false, false, null, "text");
			return false;
		}

		options = new CommandOptions(repository, stale, help, baseline, format);
		exitCode = CommandLine.Success;
		return true;
	}

	/// <summary>Reads a named option, in <c>--name value</c> or <c>--name=value</c> form.</summary>
	/// <param name="argument">The current argument.</param>
	/// <param name="args">All arguments, for the separate-value form.</param>
	/// <param name="index">The current index, advanced past a separate value.</param>
	/// <param name="name">The option name.</param>
	/// <param name="value">The value, or <see langword="null"/> when it was omitted.</param>
	/// <returns><see langword="true"/> when the argument is the named option.</returns>
	static bool TryRead(string argument, IReadOnlyList<string> args, ref int index, string name, out string? value)
	{
		if (argument == name)
		{
			value = index + 1 < args.Count ? args[++index] : null;
			return true;
		}

		var prefix = name + "=";

		if (argument.StartsWith(prefix, StringComparison.Ordinal))
		{
			value = argument[prefix.Length..];
			return true;
		}

		value = null;
		return false;
	}

	/// <summary>Writes a usage error and its usage text.</summary>
	/// <param name="message">The error.</param>
	/// <param name="usage">The usage text.</param>
	/// <param name="error">Where to write.</param>
	/// <returns>The usage exit code.</returns>
	static int Fail(string message, string usage, TextWriter error)
	{
		error.WriteLine(message);
		error.WriteLine();
		error.WriteLine(usage);
		return CommandLine.UsageError;
	}
}
