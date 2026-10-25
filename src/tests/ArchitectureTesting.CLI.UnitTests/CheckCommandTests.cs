using System.Text.Json;
using Purview.Architecture.CLI;

namespace Purview.Architecture.Testing.CLI.UnitTests;

/// <summary>Exercises <c>architests check</c>, which runs the whole rule set outside TUnit.</summary>
public sealed class CheckCommandTests
{
	[Test]
	public async Task Run_GivenACleanRepository_Succeeds()
	{
		// Arrange
		var root = CreateRepository(declareUnmappedProject: false);

		try
		{
			// Act
			var (exitCode, output, _) = ToolFixture.Run("check", "--repository", root);

			// Assert
			await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
			await Assert.That(output).Contains("respects its architecture policy");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Run_GivenAViolation_ReturnsFindings()
	{
		// Arrange
		var root = CreateRepository(declareUnmappedProject: true);

		try
		{
			// Act
			var (exitCode, output, error) = ToolFixture.Run("check", "--repository", root);

			// Assert
			await Assert.That(exitCode).IsEqualTo(CommandLine.Findings);
			await Assert.That(output).Contains("naming:");
			await Assert.That(output).Contains("Rogue");
			await Assert.That(error).Contains("architecture violation");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Run_GivenJsonFormat_WritesTheViolations()
	{
		// Arrange
		var root = CreateRepository(declareUnmappedProject: true);

		try
		{
			// Act
			var (exitCode, output, _) = ToolFixture.Run("check", "--format", "json", "--repository", root);

			// Assert
			using var document = JsonDocument.Parse(output);

			await Assert.That(exitCode).IsEqualTo(CommandLine.Findings);
			await Assert.That(document.RootElement.GetArrayLength()).IsGreaterThan(0);
			await Assert.That(document.RootElement[0].GetProperty("rule").GetString()).IsEqualTo("naming");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>
	/// The point of the SARIF output: a violation becomes an annotation on the offending project
	/// in a pull request, with the rule's own description attached.
	/// </summary>
	[Test]
	public async Task Run_GivenSarifFormat_WritesRulesAndALocation()
	{
		// Arrange
		var root = CreateRepository(declareUnmappedProject: true);

		try
		{
			// Act
			var (exitCode, output, _) = ToolFixture.Run("check", "--format", "sarif", "--repository", root);

			// Assert
			using var document = JsonDocument.Parse(output);
			var run = document.RootElement.GetProperty("runs")[0];
			var result = run.GetProperty("results")[0];
			var rule = run.GetProperty("tool").GetProperty("driver").GetProperty("rules")[0];

			await Assert.That(exitCode).IsEqualTo(CommandLine.Findings);
			await Assert.That(document.RootElement.GetProperty("version").GetString()).IsEqualTo("2.1.0");
			await Assert.That(result.GetProperty("ruleId").GetString()).IsEqualTo("naming");

			var uri = result
				.GetProperty("locations")[0]
				.GetProperty("physicalLocation")
				.GetProperty("artifactLocation")
				.GetProperty("uri")
				.GetString();

			await Assert
				.That(uri)
				.IsEqualTo("src/Rogue/Rogue.csproj")
				.Because("the finding must point at the project it is about, with forward slashes on every platform");

			await Assert
				.That(rule.GetProperty("id").GetString())
				.IsEqualTo("naming")
				.Because("the rule catalogue supplies the metadata, so it is never a second copy");

			await Assert.That(rule.GetProperty("fullDescription").GetProperty("text").GetString()).IsNotEmpty();
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>
	/// A clean run must still emit a document. A consuming step is handed a file either way, and
	/// an empty SARIF run is what clears findings that have since been fixed from code scanning.
	/// </summary>
	[Test]
	public async Task Run_GivenACleanRepositoryAndSarif_WritesAnEmptyRun()
	{
		// Arrange
		var root = CreateRepository(declareUnmappedProject: false);

		try
		{
			// Act
			var (exitCode, output, _) = ToolFixture.Run("check", "--format", "sarif", "--repository", root);

			// Assert
			using var document = JsonDocument.Parse(output);

			await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
			await Assert.That(document.RootElement.GetProperty("version").GetString()).IsEqualTo("2.1.0");
			await Assert
				.That(document.RootElement.GetProperty("runs")[0].GetProperty("results").GetArrayLength())
				.IsEqualTo(0);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Run_GivenACleanRepositoryAndJson_WritesAnEmptyArray()
	{
		// Arrange
		var root = CreateRepository(declareUnmappedProject: false);

		try
		{
			// Act
			var (exitCode, output, _) = ToolFixture.Run("check", "--format", "json", "--repository", root);

			// Assert
			using var document = JsonDocument.Parse(output);

			await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
			await Assert.That(document.RootElement.GetArrayLength()).IsEqualTo(0);
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Run_GivenAnUnknownFormat_ReturnsUsageError()
	{
		// Act
		var (exitCode, _, error) = ToolFixture.Run("check", "--format", "yaml");

		// Assert
		await Assert.That(exitCode).IsEqualTo(CommandLine.UsageError);
		await Assert.That(error).Contains("Unknown format 'yaml'");
	}

	[Test]
	public async Task Run_GivenHelp_WritesUsage()
	{
		// Act
		var (exitCode, output, _) = ToolFixture.Run("check", "--help");

		// Assert
		await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
		await Assert.That(output).Contains("architests check");
	}

	[Test]
	public async Task Run_GivenNoCommand_ListsCheck()
	{
		// Act
		var (_, output, _) = ToolFixture.Run();

		// Assert
		await Assert.That(output).Contains("check");
	}

	/// <summary>Builds a repository with one mapped project, optionally plus an unmapped one.</summary>
	/// <param name="declareUnmappedProject">Whether to add a project no responsibility matches.</param>
	/// <returns>The repository root.</returns>
	static string CreateRepository(bool declareUnmappedProject)
	{
		var root = ToolFixture.CreateRepository(ToolFixture.ValidPolicy);

		Project(root, "Core");

		if (declareUnmappedProject)
			Project(root, "Rogue");

		return root;
	}

	/// <summary>Writes an empty project file.</summary>
	/// <param name="root">The repository root.</param>
	/// <param name="name">The project name.</param>
	static void Project(string root, string name)
	{
		var folder = Path.Combine(root, "src", name);
		Directory.CreateDirectory(folder);
		File.WriteAllText(Path.Combine(folder, $"{name}.csproj"), """<Project Sdk="Microsoft.NET.Sdk"></Project>""");
	}
}
