namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Exercises the API surface and documentation rules.</summary>
public sealed class ContractRuleTests
{
	static APIRules Enabled => new() { Enabled = true };

	static ArchitecturePolicy Policy(APIRules api) =>
		new()
		{
			Version = 3,
			Responsibilities =
			[
				new Responsibility
				{
					Name = "Core",
					Category = "domain",
					Purpose = "A responsibility declared by the contract-rule fixture.",
					MayDependOn = [],
					MustNotDependOn = [],
				},
			],
			APIRules = api,
		};

	static ArchitecturePolicyEvaluator Evaluator(
		APIRules api,
		APIBaseline? baseline,
		APIType[] types,
		string assemblyPath = ""
	)
	{
		var policy = Policy(api);

		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
		{
			["Core"] = new("Core", "/repo/src/Core/Core.csproj", [], [], false) { RootNamespace = "Purview.Core" },
		};

		Dictionary<string, APIAssembly> surfaces = new(StringComparer.OrdinalIgnoreCase)
		{
			["Core"] = new("Core", "Core", [], types) { Path = assemblyPath },
		};

		return new ArchitecturePolicyEvaluator(
			policy,
			projects,
			new Dictionary<string, AssemblyNode>(),
			apiSurfaces: surfaces,
			apiBaseline: baseline
		);
	}

	static APIBaseline Capture(params APIType[] types) =>
		APIBaseline.Capture(
			new Dictionary<string, APIAssembly>(StringComparer.OrdinalIgnoreCase)
			{
				["Core"] = new("Core", "Core", [], types),
			},
			Policy(Enabled)
		);

	[Test]
	public async Task APISurfaceCompatibility_GivenNoBaseline_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(Enabled, baseline: null, [TypeRuleFixture.Type("Order")]);

		// Act
		var violations = evaluator.APISurfaceCompatibility();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Rule).IsEqualTo(ArchitectureRules.APISurface);
		await Assert.That(violations[0].Message).Contains("No API baseline was found");
	}

	[Test]
	public async Task APISurfaceCompatibility_GivenAMatchingBaseline_ReportsNothing()
	{
		// Arrange
		APIType[] types = [TypeRuleFixture.Type("Order")];
		var evaluator = Evaluator(Enabled, Capture(types), types);

		// Act
		var violations = evaluator.APISurfaceCompatibility();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task APISurfaceCompatibility_GivenAnAddedType_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(
			Enabled,
			Capture(TypeRuleFixture.Type("Order")),
			[TypeRuleFixture.Type("Order"), TypeRuleFixture.Type("Customer")]
		);

		// Act
		var violations = evaluator.APISurfaceCompatibility();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("Purview.Core.Customer");
		await Assert.That(violations[0].Message).Contains("api capture");
	}

	[Test]
	public async Task APIDocumentation_GivenNoDocumentationFile_ReportsTheProject()
	{
		// Arrange
		var evaluator = Evaluator(Enabled, baseline: null, [TypeRuleFixture.Type("Order")]);

		// Act
		var violations = evaluator.APIDocumentation();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Rule).IsEqualTo(ArchitectureRules.APIDocumentation);
		await Assert.That(violations[0].Message).Contains("XML documentation");
	}

	[Test]
	public async Task APIDocumentation_GivenAnUndocumentedType_ReportsIt()
	{
		// Arrange
		var directory = Directory.CreateTempSubdirectory("PurviewDocs").FullName;
		var assembly = Path.Combine(directory, "Core.dll");
		await File.WriteAllTextAsync(
			Path.ChangeExtension(assembly, ".xml"),
			"""<doc><members><member name="T:Purview.Core.Customer" /></members></doc>"""
		);

		try
		{
			var evaluator = Evaluator(Enabled, baseline: null, [TypeRuleFixture.Type("Order")], assembly);

			// Act
			var violations = evaluator.APIDocumentation();

			// Assert
			await Assert.That(violations).HasSingleItem();
			await Assert.That(violations[0].Message).Contains("Purview.Core.Order");
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[Test]
	public async Task APIDocumentation_GivenADocumentedType_ReportsNothing()
	{
		// Arrange
		var directory = Directory.CreateTempSubdirectory("PurviewDocs").FullName;
		var assembly = Path.Combine(directory, "Core.dll");
		await File.WriteAllTextAsync(
			Path.ChangeExtension(assembly, ".xml"),
			"""<doc><members><member name="T:Purview.Core.Order" /></members></doc>"""
		);

		try
		{
			var evaluator = Evaluator(Enabled, baseline: null, [TypeRuleFixture.Type("Order")], assembly);

			// Act
			var violations = evaluator.APIDocumentation();

			// Assert
			await Assert.That(violations).IsEmpty();
		}
		finally
		{
			Directory.Delete(directory, recursive: true);
		}
	}

	[Test]
	public async Task EnabledAPIRules_GivenTheGroupOn_ListsBothRules()
	{
		// Arrange
		var evaluator = Evaluator(Enabled, baseline: null, []);

		// Act
		var rules = evaluator.EnabledAPIRules();

		// Assert
		await Assert.That(rules).Contains(ArchitectureRules.APISurface);
		await Assert.That(rules).Contains(ArchitectureRules.APIDocumentation);
	}

	[Test]
	public async Task EnabledAPIRules_GivenTheGroupOff_ListsNothing()
	{
		// Arrange
		var evaluator = Evaluator(new APIRules { Enabled = false }, baseline: null, []);

		// Act
		var rules = evaluator.EnabledAPIRules();

		// Assert
		await Assert.That(rules).IsEmpty();
	}
}
