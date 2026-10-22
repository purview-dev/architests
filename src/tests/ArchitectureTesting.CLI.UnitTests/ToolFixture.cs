using Purview.Architecture.CLI;

namespace Purview.Architecture.Testing.CLI.UnitTests;

/// <summary>Builds a throwaway repository and drives the tool against it.</summary>
/// <remarks>
/// The commands take a real policy file on disk, so the tests use the temp directory rather than an
/// abstraction. Every path is composed with <see cref="Path.Combine(string, string)"/>, so the suite runs on
/// Windows, Linux and macOS alike.
/// </remarks>
static class ToolFixture
{
	/// <summary>A policy that passes the schema and the integrity rule.</summary>
	public const string ValidPolicy = /*lang=json,strict*/
		"""
		{
			"version": 3,
			"sourceRoots": ["src"],
			"responsibilities": [
				{
					"name": "Core",
					"category": "domain",
					"purpose": "The fundamental domain model and its rules.",
					"mayDependOn": [],
					"mustNotDependOn": []
				}
			]
		}
		""";

	/// <summary>A policy that omits the required responsibility catalogue.</summary>
	public const string SchemaBreach = /*lang=json,strict*/
		"""{ "version": 3 }""";

	/// <summary>A policy whose matrix names a responsibility that does not exist.</summary>
	public const string IntegrityBreach = /*lang=json,strict*/
		"""
		{
			"version": 3,
			"responsibilities": [
				{
					"name": "Core",
					"category": "domain",
					"purpose": "The fundamental domain model and its rules.",
					"mayDependOn": ["Missing"],
					"mustNotDependOn": []
				}
			]
		}
		""";

	/// <summary>A policy with one waiver whose review date is long past the maximum age.</summary>
	public const string StaleWaiver = /*lang=json,strict*/
		"""
		{
			"version": 3,
			"responsibilities": [
				{
					"name": "Core",
					"category": "domain",
					"purpose": "The fundamental domain model and its rules.",
					"mayDependOn": [],
					"mustNotDependOn": []
				}
			],
			"waiverPolicy": { "maxAgeDays": 30 },
			"waivers": [
				{
					"project": "Legacy.Utilities",
					"rules": ["naming"],
					"reason": "Predates the catalogue; renaming breaks a published package identifier.",
					"reviewedOn": "2020-01-01"
				}
			]
		}
		""";

	/// <summary>Creates a repository holding one policy file.</summary>
	/// <param name="policy">The policy document.</param>
	/// <returns>The repository root.</returns>
	public static string CreateRepository(string policy)
	{
		var root = Directory.CreateTempSubdirectory("PurviewArchitestsCli").FullName;
		Directory.CreateDirectory(Path.Combine(root, ".config"));
		File.WriteAllText(Path.Combine(root, ArchitecturePolicy.RelativePath), policy);

		return root;
	}

	/// <summary>Runs the tool with captured output.</summary>
	/// <param name="args">The command-line arguments.</param>
	/// <returns>The exit code, standard output and standard error.</returns>
	public static (int ExitCode, string Output, string Error) Run(params string[] args)
	{
		using StringWriter output = new();
		using StringWriter error = new();
		var exitCode = CommandLine.Run(args, output, error);

		return (exitCode, output.ToString(), error.ToString());
	}
}
