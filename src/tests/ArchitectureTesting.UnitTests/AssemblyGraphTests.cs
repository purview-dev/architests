namespace Purview.Architecture.Testing.UnitTests;

/// <summary>
/// Exercises the metadata-only assembly reader.
/// </summary>
/// <remarks>
/// The fixtures are real assemblies - this test assembly, the package assembly and a package - so the
/// reader is validated against genuine metadata rather than a hand-built stub. No assembly is loaded
/// or executed, which is the property the reader exists to guarantee.
/// </remarks>
public sealed class AssemblyGraphTests
{
	static ProjectNode Project(string name) => new(name, $"/repo/src/{name}/{name}.csproj", [], [], false);

	[Test]
	public async Task AssemblyGraph_GivenAnAssemblyNamedExactlyForItsProject_ResolvesTheProjectAndItsReferences()
	{
		// Arrange
		// The test project has no SDK marker (test projects do not get one), so its assembly is
		// attributed by the exact-name fallback. Its reference to the package assembly resolves
		// against the scanned set.
		var testPath = typeof(AssemblyGraphTests).Assembly.Location;
		var packagePath = typeof(ArchitecturePolicyEvaluator).Assembly.Location;
		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
		{
			["Purview.Architecture.ArchitectureTesting.UnitTests"] = Project(
				"Purview.Architecture.ArchitectureTesting.UnitTests"
			),
			["ArchitectureTesting"] = Project("ArchitectureTesting"),
		};

		// Act
		var assemblies = AssemblyGraph.Load([testPath, packagePath], projects);

		// Assert
		await Assert.That(assemblies.ContainsKey("Purview.Architecture.ArchitectureTesting.UnitTests")).IsTrue();
		await Assert
			.That(
				assemblies["Purview.Architecture.ArchitectureTesting.UnitTests"]
					.References.Any(reference =>
						reference.TargetProject == "ArchitectureTesting"
						&& reference.TypeName.Contains("ArchitecturePolicyEvaluator", StringComparison.Ordinal)
					)
			)
			.IsTrue();
	}

	[Test]
	public async Task AssemblyGraph_GivenAnExplicitAssemblyName_UsesTheSdkMarkerToResolveTheProject()
	{
		// Arrange
		// The package's project is ArchitectureTesting but its assembly is Purview.Architecture.Testing,
		// so the project name is not a suffix of the assembly - the SDK's generated marker identifies it.
		var path = typeof(ArchitecturePolicyEvaluator).Assembly.Location;
		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
		{
			["ArchitectureTesting"] = Project("ArchitectureTesting"),
		};

		// Act
		var assemblies = AssemblyGraph.Load([path], projects);

		// Assert
		await Assert.That(assemblies.ContainsKey("ArchitectureTesting")).IsTrue();
	}

	[Test]
	public async Task AssemblyGraph_GivenAnAssemblyThatMapsToNoProject_IgnoresIt()
	{
		// Arrange
		var path = typeof(AssemblyGraphTests).Assembly.Location;
		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
		{
			["SomethingElse"] = Project("SomethingElse"),
		};

		// Act
		var assemblies = AssemblyGraph.Load([path], projects);

		// Assert
		await Assert.That(assemblies).IsEmpty();
	}

	[Test]
	public async Task AssemblyGraph_GivenAPackageWhoseNameEndsWithAProjectName_DoesNotMisattributeIt()
	{
		// Arrange
		// 'TUnit.Core' ends with the project name 'Core', but it is a package, not the project. A
		// suffix match would attribute its types to Core and report dependencies that do not exist.
		var path = typeof(TestAttribute).Assembly.Location;
		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase) { ["Core"] = Project("Core") };

		// Act
		var assemblies = AssemblyGraph.Load([path], projects);

		// Assert
		await Assert.That(assemblies).IsEmpty();
	}
}
