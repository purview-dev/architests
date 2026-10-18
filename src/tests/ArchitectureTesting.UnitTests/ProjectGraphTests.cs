namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Exercises the project reader, including the resolved package closure.</summary>
public sealed class ProjectGraphTests
{
	[Test]
	public async Task Load_GivenARestoreAssetsFile_ReadsTheResolvedClosure()
	{
		// Arrange
		var root = CreateRepository(
			/*lang=json,strict*/
			"""
			{
				"targets": {
					"net10.0": {
						"Declared.Package/1.0.0": { "type": "package" },
						"Transitive.Package/2.0.0": { "type": "package" },
						"Core/1.0.0": { "type": "project" }
					}
				}
			}
			"""
		);

		try
		{
			// Act
			var projects = ProjectGraph.Load(root, ["src"]);
			var core = projects["Core"];

			// Assert
			await Assert.That(core.ResolvedPackages).Contains("Declared.Package");
			await Assert.That(core.ResolvedPackages).Contains("Transitive.Package");
			await Assert.That(core.ResolvedPackages).DoesNotContain("Core");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Load_GivenNoAssetsFile_ReadsNoResolvedPackages()
	{
		// Arrange
		var root = CreateRepository(assets: null);

		try
		{
			// Act
			var projects = ProjectGraph.Load(root, ["src"]);

			// Assert
			await Assert.That(projects["Core"].ResolvedPackages).IsEmpty();
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task ForbiddenPackages_GivenATransitivePackage_ReportsIt()
	{
		// Arrange
		// The csproj declares nothing; restore pulled the package in transitively.
		var root = CreateRepository(
			/*lang=json,strict*/
			"""
			{
				"targets": {
					"net10.0": {
						"Contoso.Forbidden.Provider/1.0.0": { "type": "package" }
					}
				}
			}
			"""
		);

		try
		{
			ArchitecturePolicy policy = new()
			{
				Version = 3,
				Responsibilities =
				[
					new Responsibility
					{
						Name = "Core",
						Category = "domain",
						Purpose = "A responsibility declared by the project-graph fixture.",
						MayDependOn = [],
						MustNotDependOn = [],
						ForbiddenPackagePrefixes = ["Contoso.Forbidden"],
					},
				],
			};

			var projects = ProjectGraph.Load(root, ["src"]);
			ArchitecturePolicyEvaluator evaluator = new(policy, projects);

			// Act
			var violations = evaluator.ForbiddenPackages();

			// Assert
			await Assert.That(violations).HasSingleItem();
			await Assert.That(violations[0].Message).Contains("Contoso.Forbidden.Provider");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>Writes a repository with one project and, optionally, its assets file.</summary>
	/// <param name="assets">The assets document, or <see langword="null"/> to omit it.</param>
	/// <returns>The repository root.</returns>
	static string CreateRepository(string? assets)
	{
		var root = Directory.CreateTempSubdirectory("PurviewProjectGraph").FullName;
		var project = Path.Combine(root, "src", "Core");
		Directory.CreateDirectory(project);

		File.WriteAllText(Path.Combine(project, "Core.csproj"), """<Project Sdk="Microsoft.NET.Sdk"></Project>""");

		if (assets is not null)
		{
			var obj = Path.Combine(project, "obj");
			Directory.CreateDirectory(obj);
			File.WriteAllText(Path.Combine(obj, "project.assets.json"), assets);
		}

		return root;
	}
}
