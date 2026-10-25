using System.Text.Json;
using System.Text.Json.Nodes;
using Purview.Architecture.CLI;

namespace Purview.Architecture.Testing.CLI.UnitTests;

/// <summary>Exercises <c>architests api capture</c> and <c>api check</c>.</summary>
public sealed class APICommandTests
{
	[Test]
	public async Task Run_GivenAPICapture_WritesABaseline()
	{
		// Arrange
		var root = CreateRepository();

		try
		{
			// Act
			var (exitCode, output, _) = ToolFixture.Run("api", "capture", "--repository", root);

			// Assert
			var baseline = Path.Combine(root, ".config", "api-surface.json");
			await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
			await Assert.That(output).Contains("Captured");
			await Assert.That(File.Exists(baseline)).IsTrue();
			await Assert.That(await File.ReadAllTextAsync(baseline)).Contains("ArchitectureTesting");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Run_GivenAPICheckAndAnUnchangedBaseline_Succeeds()
	{
		// Arrange
		var root = CreateRepository();

		try
		{
			ToolFixture.Run("api", "capture", "--repository", root);

			// Act
			var (exitCode, output, _) = ToolFixture.Run("api", "check", "--repository", root);

			// Assert
			await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
			await Assert.That(output).Contains("API matches the baseline");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Run_GivenAPICheckAndATamperedBaseline_ReturnsFindings()
	{
		// Arrange
		var root = CreateRepository();

		try
		{
			ToolFixture.Run("api", "capture", "--repository", root);

			var baseline = Path.Combine(root, ".config", "api-surface.json");
			var text = await File.ReadAllTextAsync(baseline);
			await File.WriteAllTextAsync(
				baseline,
				text.Replace(
					"Purview.Architecture.Testing.ArchitecturePolicy\"",
					"Purview.Architecture.Testing.ArchitecturePolicyGone\"",
					StringComparison.Ordinal
				)
			);

			// Act
			var (exitCode, output, error) = ToolFixture.Run("api", "check", "--repository", root);

			// Assert
			await Assert.That(exitCode).IsEqualTo(CommandLine.Findings);
			await Assert.That(output).Contains("ArchitecturePolicyGone");
			await Assert.That(error).Contains("difference");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Run_GivenAPIcheckWithSarifAndADifference_WritesASarifDocument()
	{
		// Arrange
		var root = CreateRepository();

		try
		{
			ToolFixture.Run("api", "capture", "--repository", root);

			var baseline = Path.Combine(root, ".config", "api-surface.json");
			var text = await File.ReadAllTextAsync(baseline);
			await File.WriteAllTextAsync(
				baseline,
				text.Replace(
					"Purview.Architecture.Testing.ArchitecturePolicy\"",
					"Purview.Architecture.Testing.ArchitecturePolicyGone\"",
					StringComparison.Ordinal
				)
			);

			// Act
			var (exitCode, output, _) = ToolFixture.Run("api", "check", "--format", "sarif", "--repository", root);

			// Assert
			using var document = JsonDocument.Parse(output);
			var rootElement = document.RootElement;

			await Assert.That(exitCode).IsEqualTo(CommandLine.Findings);
			await Assert.That(rootElement.GetProperty("version").GetString()).IsEqualTo("2.1.0");
			await Assert.That(rootElement.TryGetProperty("$schema", out _)).IsTrue();

			var result = rootElement.GetProperty("runs")[0].GetProperty("results")[0];
			await Assert.That(result.GetProperty("ruleId").GetString()).IsEqualTo("apiSurface");
			await Assert.That(result.GetProperty("level").GetString()).IsEqualTo("error");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>
	/// The reported crash. A baseline holding two overloads that share a kind, name and parameter
	/// list - one generic, one not - used to throw <see cref="ArgumentException"/> out of the
	/// comparison before any difference could be reported.
	/// </summary>
	[Test]
	public async Task Run_GivenAPICheckAndOverloadsSharingAParameterList_ReportsDriftRatherThanThrowing()
	{
		// Arrange
		var root = CreateRepository();

		try
		{
			ToolFixture.Run("api", "capture", "--repository", root);

			var baseline = Path.Combine(root, ".config", "api-surface.json");
			var document = JsonNode.Parse(await File.ReadAllTextAsync(baseline))!;
			var members = document["projects"]!
				.AsArray()
				.SelectMany(project => project!["types"]!.AsArray())
				.Select(type => type!["members"]?.AsArray())
				.OfType<JsonArray>()
				.First(candidate => candidate.Count > 0);

			// A member the build declares but the baseline no longer does, to prove the rest of the
			// surface is still compared. Taken before the pair is added, so it is a real member.
			var removed = members[0]!["name"]!.GetValue<string>();
			members.RemoveAt(0);

			// The colliding pair: identical but for arity and return type. Neither is in the build,
			// so both are drift the comparison must report.
			members.Add(Member("Purview.Results.Result<!!0, System.String>", genericArity: 1));
			members.Add(Member("Purview.Results.Result<System.String>"));

			await File.WriteAllTextAsync(baseline, document.ToJsonString());

			// Act
			var (exitCode, output, error) = ToolFixture.Run("api", "check", "--repository", root);

			// Assert
			await Assert.That(exitCode).IsEqualTo(CommandLine.Findings);
			await Assert.That(error).Contains("difference");
			await Assert
				.That(output)
				.Contains("AsFailure<T>")
				.Because("the generic overload must be named by its arity");
			await Assert.That(output).Contains("AsFailure(System.String)");
			await Assert.That(output).Contains(removed).Because("real drift elsewhere must still be reported");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>Builds a baseline member entry for the overload fixture.</summary>
	/// <param name="typeName">The declared return type.</param>
	/// <param name="genericArity">The number of generic parameters.</param>
	/// <returns>The entry.</returns>
	static JsonObject Member(string typeName, int genericArity = 0) =>
		new()
		{
			["kind"] = "Method",
			["name"] = "AsFailure",
			["typeName"] = typeName,
			["isStatic"] = true,
			["parameters"] = new JsonArray("System.String"),
			["genericArity"] = genericArity,
		};

	[Test]
	public async Task Run_GivenAPIWithoutAVerb_ReturnsUsageError()
	{
		// Act
		var (exitCode, _, error) = ToolFixture.Run("api");

		// Assert
		await Assert.That(exitCode).IsEqualTo(CommandLine.UsageError);
		await Assert.That(error).Contains("capture");
	}

	[Test]
	public async Task Run_GivenAPICheckWithAnUnknownFormat_ReturnsUsageError()
	{
		// Act
		var (exitCode, _, error) = ToolFixture.Run("api", "check", "--format", "yaml");

		// Assert
		await Assert.That(exitCode).IsEqualTo(CommandLine.UsageError);
		await Assert.That(error).Contains("Unknown format 'yaml'");
	}

	/// <summary>Builds a repository shaped like a real one, holding a copy of the package assembly.</summary>
	/// <returns>The repository root.</returns>
	/// <remarks>
	/// The assembly is copied into the fixture's own <c>bin</c> folder rather than the policy
	/// pointing at this test's output directory. Paths in a policy are repo-relative and may not
	/// escape the repository - see <see cref="PolicyPath"/> - so an absolute root outside it is
	/// rejected, which is the behaviour this fixture would otherwise have to work around.
	/// </remarks>
	static string CreateRepository()
	{
		var root = Directory.CreateTempSubdirectory("PurviewAPICli").FullName;
		var config = Path.Combine(root, ".config");
		Directory.CreateDirectory(config);

		const string policy = """
			{
				"version": 3,
				"sourceRoots": ["src"],
				"responsibilities": [
					{
						"name": "ArchitectureTesting",
						"category": "shared",
						"purpose": "The package assembly this test uses as its API fixture.",
						"mayDependOn": [],
						"mustNotDependOn": []
					}
				],
				"assemblyRules": { "assemblyRoots": ["src"] },
				"apiRules": { "enabled": true }
			}
			""";

		File.WriteAllText(Path.Combine(config, "architecture-policy.json"), policy);

		var project = Path.Combine(root, "src", "ArchitectureTesting");
		var output = Path.Combine(project, "bin");
		Directory.CreateDirectory(output);

		File.WriteAllText(
			Path.Combine(project, "ArchitectureTesting.csproj"),
			"""<Project Sdk="Microsoft.NET.Sdk"></Project>"""
		);

		// The package assembly, plus its documentation so the apiDocumentation rule has something
		// to read. Attribution finds the project through the SDK's generated assembly marker, as
		// it would in a real repository.
		var source = typeof(ArchitecturePolicy).Assembly.Location;
		File.Copy(source, Path.Combine(output, Path.GetFileName(source)));

		var documentation = Path.ChangeExtension(source, ".xml");
		if (File.Exists(documentation))
			File.Copy(documentation, Path.Combine(output, Path.GetFileName(documentation)));

		return root;
	}
}
