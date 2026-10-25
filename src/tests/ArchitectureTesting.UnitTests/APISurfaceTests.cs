namespace Purview.Architecture.Testing.UnitTests;

/// <summary>
/// Exercises the API-surface reader against a real compiled assembly.
/// </summary>
/// <remarks>
/// The rules are tested against in-memory fixtures; the reader is tested against this test assembly
/// and the package it references, so a metadata bug cannot hide behind a hand-built model.
/// </remarks>
public sealed class APISurfaceTests
{
	static readonly string PackageAssembly = typeof(APISurface).Assembly.Location;

	static IReadOnlyList<APIType> PackageTypes => APISurface.Read(PackageAssembly);

	static APIType PackageType(string name) =>
		PackageTypes.Single(type => type.Name == name && type.Namespace == "Purview.Architecture.Testing");

	[Test]
	public async Task Read_GivenThePackageAssembly_DescribesAPublicSealedClass()
	{
		// Act
		var type = PackageType("ArchitecturePolicyEvaluator");

		// Assert
		await Assert.That(type.Kind).IsEqualTo(APITypeKind.Class);
		await Assert.That(type.IsPublic).IsTrue();
		await Assert.That(type.IsSealed).IsTrue();
		await Assert.That(type.IsAbstract).IsFalse();
		await Assert.That(type.Namespace).IsEqualTo("Purview.Architecture.Testing");
	}

	[Test]
	public async Task Read_GivenAProperty_ReportsItsDeclaredType()
	{
		// Arrange
		var policy = PackageType("ArchitecturePolicy");

		// Act
		var waivers = policy.Members.Single(member => member.Name == "Waivers");

		// Assert
		await Assert.That(waivers.Kind).IsEqualTo(APIMemberKind.Property);
		await Assert.That(waivers.TypeName).Contains("System.Collections.Generic.IReadOnlyList<");
		await Assert.That(waivers.IsStatic).IsFalse();
	}

	[Test]
	public async Task Read_GivenAnInterface_ReportsTheInterfaceKindAndItsIPrefix()
	{
		// Act
		var type = PackageType("IArchitectureRule");

		// Assert
		await Assert.That(type.Kind).IsEqualTo(APITypeKind.Interface);
		await Assert.That(type.Name.StartsWith('I')).IsTrue();
		await Assert.That(type.IsPublic).IsTrue();
	}

	[Test]
	public async Task Read_GivenAStaticClass_ReportsItAsAbstractAndSealed()
	{
		// Act
		var type = PackageType("ProjectGraph");

		// Assert
		await Assert.That(type.IsStatic).IsTrue();
		await Assert.That(type.IsAbstract).IsTrue();
		await Assert.That(type.IsSealed).IsTrue();
	}

	[Test]
	public async Task Read_GivenAnAttributeWithStringArguments_DecodesTheFirstOne()
	{
		// Arrange
		// ArchitecturePolicy.Version carries [JsonPropertyName("version")].
		var type = PackageType("ArchitecturePolicy");
		var property = type.Members.Single(member => member.Name == "Version");

		// Act
		var attribute = property.Attributes.Single(candidate =>
			candidate.TypeName == "System.Text.Json.Serialization.JsonPropertyNameAttribute"
		);

		// Assert
		await Assert.That(attribute.StringArgument).IsEqualTo("version");
	}

	/// <summary>
	/// Reads the container the compiler emits for <see cref="ExtensionFixture"/>'s extension block,
	/// from real metadata rather than a hand-built model.
	/// </summary>
	/// <remarks>
	/// The assertion that matters is that at least one container is synthesized and carries neither
	/// <c>[CompilerGenerated]</c> nor <c>[GeneratedCode]</c>: that is the type the attribute check
	/// alone lets through and the type rules then report unactionably.
	/// </remarks>
	[Test]
	public async Task Read_GivenAnExtensionBlockContainer_ReportsItAsCompilerSynthesized()
	{
		// Arrange
		var types = APISurface.Read(typeof(APISurfaceTests).Assembly.Location);

		// Act
		var containers = types
			.Where(type => type.FullName.Contains("ExtensionFixture", StringComparison.Ordinal))
			.ToArray();

		// Assert
		await Assert
			.That(containers.Any(type => type.IsCompilerSynthesized))
			.IsTrue()
			.Because("the compiler emits a <G>$… container for an extension block");

		await Assert
			.That(containers.Any(type => type.IsCompilerSynthesized && !type.IsGeneratedCode))
			.IsTrue()
			.Because("a container the attribute check misses is the whole reason the name check exists");
	}

	/// <summary>A hand-written type is not mistaken for a synthesized one.</summary>
	[Test]
	public async Task Read_GivenAHandWrittenType_IsNotCompilerSynthesized()
	{
		// Act
		var type = PackageType("ArchitecturePolicy");

		// Assert
		await Assert.That(type.IsCompilerSynthesized).IsFalse();
	}

	[Test]
	public async Task ReadAssembly_GivenAProjectAssembly_AttributesItThroughTheSdkMarker()
	{
		// Arrange
		// The package's assembly name is not its project name, so attribution must use the marker.
		// Test projects suppress the marker, which is why this uses a production-shaped assembly.
		var projectName = "ArchitectureTesting";
		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
		{
			[projectName] = new(projectName, "/repo/src/ArchitectureTesting/x.csproj", [], [], false),
		};

		// Act
		var surface = APISurface.ReadAssembly(PackageAssembly, projects);

		// Assert
		await Assert.That(surface).IsNotNull();
		await Assert.That(surface!.ProjectName).IsEqualTo(projectName);
		await Assert.That(surface.Types).IsNotEmpty();
	}

	[Test]
	public async Task ReadAssembly_GivenANonProjectAssembly_ReturnsNothing()
	{
		// Arrange
		// The package assembly maps to no project in this dictionary, so it is a package.
		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
		{
			["Unrelated"] = new("Unrelated", "/repo/src/Unrelated/Unrelated.csproj", [], [], false),
		};

		// Act
		var surface = APISurface.ReadAssembly(PackageAssembly, projects);

		// Assert
		await Assert.That(surface).IsNull();
	}

	[Test]
	public async Task Read_GivenAFileThatIsNotAnAssembly_ReturnsNothing()
	{
		// Arrange
		var path = Path.Combine(Path.GetTempPath(), $"PurviewAPISurface{Guid.NewGuid():N}.dll");
		await File.WriteAllTextAsync(path, "this is not a portable executable");

		try
		{
			// Act
			var types = APISurface.Read(path);

			// Assert
			await Assert.That(types).IsEmpty();
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Test]
	public async Task Read_GivenThePackageAssembly_DoesNotReturnTheSyntheticModuleType()
	{
		// Act
		var modules = PackageTypes.Where(type => type.Name == "<Module>").ToArray();

		// Assert
		await Assert.That(modules).IsEmpty();
	}
}
