namespace Purview.Architecture.Testing.UnitTests;

/// <summary>
/// Exercises the friend-assembly check and the type-rule dispatch.
/// </summary>
public sealed class AttributePolicyFriendTests
{
	[Test]
	public async Task AttributePolicy_GivenInternalsVisibleToProduction_ReportsIt()
	{
		// Arrange
		var evaluator = EvaluatorWithFriend("Other", isTest: false);

		// Act
		var violations = evaluator.AttributePolicy();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("production assembly 'Other'");
	}

	[Test]
	public async Task AttributePolicy_GivenInternalsVisibleToATestProject_ReportsNothing()
	{
		// Arrange
		var evaluator = EvaluatorWithFriend("Other", isTest: true);

		// Act
		var violations = evaluator.AttributePolicy();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task EnabledTypeRules_GivenTypeRulesOn_ListsEveryTypeRule()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(TypeRuleFixture.Rules);

		// Act
		var rules = evaluator.EnabledTypeRules();

		// Assert
		await Assert.That(rules.Count).IsEqualTo(4);
		await Assert.That(rules).Contains(ArchitectureRules.TypeNaming);
		await Assert.That(rules).Contains(ArchitectureRules.AttributePolicy);
	}

	[Test]
	public async Task EnabledTypeRules_GivenTypeRulesOff_ListsNothing()
	{
		// Arrange
		// A disabled group must produce no test, not a test that checks nothing.
		var evaluator = TypeRuleFixture.Evaluator(new TypeRules { Enabled = false });

		// Act
		var rules = evaluator.EnabledTypeRules();

		// Assert
		await Assert.That(rules).IsEmpty();
	}

	[Test]
	public async Task EvaluateTypeRule_GivenAnUnknownRule_ReportsNothing()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(TypeRuleFixture.Rules);

		// Act
		var violations = evaluator.EvaluateTypeRule("notATypeRule");

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	/// <summary>Builds an evaluator whose assembly friends another project.</summary>
	/// <param name="friend">The friend's project and assembly name.</param>
	/// <param name="isTest">Whether the friend is a test project.</param>
	/// <returns>The evaluator.</returns>
	static ArchitecturePolicyEvaluator EvaluatorWithFriend(string friend, bool isTest)
	{
		ArchitecturePolicy policy = new()
		{
			Version = 3,
			Responsibilities =
			[
				new Responsibility
				{
					Name = "Core",
					Category = "shared",
					Purpose = "A responsibility declared by the attribute-rule fixture.",
					MayDependOn = [],
					MustNotDependOn = [],
				},
				new Responsibility
				{
					Name = friend,
					Category = "shared",
					Purpose = "The friend project declared by the attribute-rule fixture.",
					MayDependOn = [],
					MustNotDependOn = [],
				},
			],
			TypeRules = TypeRuleFixture.Rules,
		};

		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
		{
			["Core"] = new("Core", "/repo/src/Core/Core.csproj", [], [], false) { RootNamespace = "Purview.Core" },
			[friend] = new(friend, $"/repo/src/{friend}/{friend}.csproj", [], [], isTest)
			{
				RootNamespace = $"Purview.{friend}",
			},
		};

		Dictionary<string, APIAssembly> surfaces = new(StringComparer.OrdinalIgnoreCase)
		{
			["Core"] = new(
				"Core",
				"Core",
				[new APIAttribute("System.Runtime.CompilerServices.InternalsVisibleToAttribute", friend)],
				[]
			),
		};

		return new ArchitecturePolicyEvaluator(
			policy,
			projects,
			new Dictionary<string, AssemblyNode>(),
			apiSurfaces: surfaces
		);
	}
}
