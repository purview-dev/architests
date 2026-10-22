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
	public async Task Load_GivenAProjectReference_ReadsThePackagesItContributes()
	{
		// Arrange
		// Direct.Package pulls Transitive.Package. Persistence (a project) pulls EF Core. The
		// project reference is not a package, so it explains EF rather than owning it.
		var root = CreateRepository(
			/*lang=json,strict*/
			"""
			{
				"targets": {
					"net10.0": {
						"Direct.Package/1.0.0": {
							"type": "package",
							"dependencies": { "Transitive.Package": "2.0.0" }
						},
						"Transitive.Package/2.0.0": { "type": "package" },
						"Persistence/1.0.0": {
							"type": "project",
							"dependencies": { "Microsoft.EntityFrameworkCore": "9.0.0" }
						},
						"Microsoft.EntityFrameworkCore/9.0.0": {
							"type": "package",
							"dependencies": { "Microsoft.EntityFrameworkCore.Abstractions": "9.0.0" }
						},
						"Microsoft.EntityFrameworkCore.Abstractions/9.0.0": { "type": "package" }
					}
				},
				"project": {
					"frameworks": {
						"net10.0": {
							"dependencies": {
								"Direct.Package": { "target": "Package", "version": "[1.0.0, )" },
								"Persistence": { "target": "Project" }
							}
						}
					}
				}
			}
			"""
		);

		try
		{
			// Act
			var core = ProjectGraph.Load(root, ["src"])["Core"];
			var target = core.PackageTargets.Single();

			// Assert
			await Assert.That(target.Direct).Contains("Direct.Package");
			await Assert.That(target.Direct).DoesNotContain("Persistence");
			await Assert.That(target.Owned).Contains("Direct.Package");
			await Assert.That(target.Owned).Contains("Transitive.Package");
			await Assert.That(target.Owned).DoesNotContain("Microsoft.EntityFrameworkCore");
			await Assert.That(target.ThroughReferences["Persistence"]).Contains("Microsoft.EntityFrameworkCore");
			await Assert
				.That(target.ThroughReferences["Persistence"])
				.Contains("Microsoft.EntityFrameworkCore.Abstractions");
			await Assert.That(core.ResolvedPackages).Contains("Microsoft.EntityFrameworkCore");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Load_GivenAnSdkDerivedAssemblyName_MapsTheReferenceBackToItsProject()
	{
		// Arrange
		// Restore keys the project node by assembly name; the SDK prefixes it with the namespace.
		var root = CreateRepository(
			/*lang=json,strict*/
			"""
			{
				"targets": {
					"net10.0": {
						"Purview.Architecture.Persistence/1.2.0": {
							"type": "project",
							"dependencies": { "Microsoft.EntityFrameworkCore": "9.0.0" }
						},
						"Microsoft.EntityFrameworkCore/9.0.0": { "type": "package" }
					}
				}
			}
			""",
			extraProject: "Persistence"
		);

		try
		{
			// Act
			var core = ProjectGraph.Load(root, ["src"])["Core"];
			var target = core.PackageTargets.Single();

			// Assert
			await Assert.That(target.ThroughReferences.ContainsKey("Persistence")).IsTrue();
			await Assert.That(target.ThroughReferences["Persistence"]).Contains("Microsoft.EntityFrameworkCore");
			await Assert.That(target.ThroughReferences.ContainsKey("Purview.Architecture.Persistence")).IsFalse();
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
	/// <param name="extraProject">An optional additional project name.</param>
	/// <returns>The repository root.</returns>
	static string CreateRepository(string? assets, string? extraProject = null)
	{
		var root = Directory.CreateTempSubdirectory("PurviewProjectGraph").FullName;
		var project = Path.Combine(root, "src", "Core");
		Directory.CreateDirectory(project);

		File.WriteAllText(Path.Combine(project, "Core.csproj"), """<Project Sdk="Microsoft.NET.Sdk"></Project>""");

		if (extraProject is not null)
		{
			var extra = Path.Combine(root, "src", extraProject);
			Directory.CreateDirectory(extra);
			File.WriteAllText(
				Path.Combine(extra, $"{extraProject}.csproj"),
				"""<Project Sdk="Microsoft.NET.Sdk"></Project>"""
			);
		}

		if (assets is not null)
		{
			var obj = Path.Combine(project, "obj");
			Directory.CreateDirectory(obj);
			File.WriteAllText(Path.Combine(obj, "project.assets.json"), assets);
		}

		return root;
	}
}
