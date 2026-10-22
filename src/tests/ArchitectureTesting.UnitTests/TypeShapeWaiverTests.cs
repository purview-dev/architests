namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Exercises the remaining type-shape checks and the waiver path.</summary>
public sealed class TypeShapeWaiverTests
{
	[Test]
	public async Task TypeShape_GivenAUtilityClassThatIsNotStatic_ReportsIt()
	{
		// Arrange
		// Only static members and no public constructor: the class should be static.
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type(
				"OrderFormatter",
				members: [TypeRuleFixture.Member("Format", isStatic: true, typeName: "System.String")]
			)
		);

		// Act
		var violations = evaluator.TypeShape();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("Declare it static");
	}

	[Test]
	public async Task TypeShape_GivenAUtilityClassWithAPublicConstructor_ReportsNothing()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type(
				"OrderFormatter",
				members:
				[
					TypeRuleFixture.Member(".ctor", APIMemberKind.Constructor),
					TypeRuleFixture.Member("Format", isStatic: true, typeName: "System.String"),
				]
			)
		);

		// Act
		var violations = evaluator.TypeShape();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task TypeShape_GivenAnExceptionNameThatIsNotAnException_ReportsIt()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type("OrderException", baseType: "System.Object")
		);

		// Act
		var violations = evaluator.TypeShape();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("does not derive from System.Exception");
	}

	[Test]
	public async Task TypeShape_GivenAWaiver_ExcusesTheRuleForTheProject()
	{
		// Arrange
		ArchitecturePolicy policy = new()
		{
			Version = 3,
			Responsibilities =
			[
				new Responsibility
				{
					Name = "Core",
					Category = "shared",
					Purpose = "A responsibility declared by the type-rule fixture.",
					MayDependOn = [],
					MustNotDependOn = [],
				},
			],
			TypeRules = TypeRuleFixture.Rules,
			Waivers =
			[
				new PolicyWaiver
				{
					Project = "Core",
					Rules = [ArchitectureRules.TypeShape],
					Reason = "The proxy requires an inheritable entity, and the waiver records that decision.",
					ReviewedOn = "2026-10-18",
				},
			],
		};

		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
		{
			["Core"] = new("Core", "/repo/src/Core/Core.csproj", [], [], false) { RootNamespace = "Purview.Core" },
		};

		Dictionary<string, APIAssembly> surfaces = new(StringComparer.OrdinalIgnoreCase)
		{
			["Core"] = new("Core", "Core", [], [TypeRuleFixture.Type("Order", isSealed: false)]),
		};

		ArchitecturePolicyEvaluator evaluator = new(
			policy,
			projects,
			new Dictionary<string, AssemblyNode>(),
			apiSurfaces: surfaces
		);

		// Act
		var violations = evaluator.TypeShape();

		// Assert
		await Assert.That(violations).IsEmpty();
	}
}
