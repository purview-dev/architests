using Purview.Architecture.CLI;

namespace Purview.Architecture.Testing.CLI.UnitTests;

/// <summary>Exercises <c>architests graph</c>.</summary>
public sealed class GraphCommandTests
{
	[Test]
	public async Task Run_GivenTextFormat_RendersProjectsAndEdges()
	{
		// Arrange
		var root = CreateRepository();

		try
		{
			// Act
			var (exitCode, output, _) = ToolFixture.Run("graph", "--repository", root);

			// Assert
			await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
			await Assert.That(output).Contains("Application (Application, application)");
			await Assert.That(output).Contains("  -> Core");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Run_GivenMermaidFormat_RendersAFlowchart()
	{
		// Arrange
		var root = CreateRepository();

		try
		{
			// Act
			var (exitCode, output, _) = ToolFixture.Run("graph", "--format", "mermaid", "--repository", root);

			// Assert
			await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
			await Assert.That(output).Contains("graph LR");
			await Assert.That(output).Contains("Application --> Core");
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
		var (exitCode, _, error) = ToolFixture.Run("graph", "--format", "dot");

		// Assert
		await Assert.That(exitCode).IsEqualTo(CommandLine.UsageError);
		await Assert.That(error).Contains("Unknown format 'dot'");
	}

	/// <summary>Writes a two-project repository with a policy.</summary>
	/// <returns>The repository root.</returns>
	static string CreateRepository()
	{
		var root = Directory.CreateTempSubdirectory("PurviewGraphCli").FullName;
		var config = Path.Combine(root, ".config");
		Directory.CreateDirectory(config);

		File.WriteAllText(
			Path.Combine(config, "architecture-policy.json"),
			/*lang=json,strict*/
			"""
			{
				"version": 3,
				"sourceRoots": ["src"],
				"responsibilities": [
					{
						"name": "Core",
						"category": "domain",
						"purpose": "The domain model used by the graph fixture.",
						"mayDependOn": [],
						"mustNotDependOn": []
					},
					{
						"name": "Application",
						"category": "application",
						"purpose": "The use-case layer used by the graph fixture.",
						"mayDependOn": ["Core"],
						"mustNotDependOn": []
					}
				]
			}
			"""
		);

		var core = Path.Combine(root, "src", "Core");
		Directory.CreateDirectory(core);
		File.WriteAllText(Path.Combine(core, "Core.csproj"), """<Project Sdk="Microsoft.NET.Sdk"></Project>""");

		var application = Path.Combine(root, "src", "Application");
		Directory.CreateDirectory(application);
		File.WriteAllText(
			Path.Combine(application, "Application.csproj"),
			"""
			<Project Sdk="Microsoft.NET.Sdk">
			  <ItemGroup>
			    <ProjectReference Include="../Core/Core.csproj" />
			  </ItemGroup>
			</Project>
			"""
		);

		return root;
	}
}
