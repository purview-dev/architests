using System.Text.Json;
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

	/// <summary>Builds a repository whose assemblies are this test's own output.</summary>
	/// <returns>The repository root.</returns>
	static string CreateRepository()
	{
		var root = Directory.CreateTempSubdirectory("PurviewAPICli").FullName;
		var config = Path.Combine(root, ".config");
		Directory.CreateDirectory(config);

		var assemblyRoot = AppContext.BaseDirectory.Replace('\\', '/');
		var policy = $$"""
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
				"assemblyRules": { "assemblyRoots": ["{{assemblyRoot}}"] },
				"apiRules": { "enabled": true }
			}
			""";

		File.WriteAllText(Path.Combine(config, "architecture-policy.json"), policy);

		var project = Path.Combine(root, "src", "ArchitectureTesting");
		Directory.CreateDirectory(project);
		File.WriteAllText(
			Path.Combine(project, "ArchitectureTesting.csproj"),
			"""<Project Sdk="Microsoft.NET.Sdk"></Project>"""
		);

		return root;
	}
}
