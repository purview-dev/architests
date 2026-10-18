namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Exercises the type-naming rule against in-memory surfaces.</summary>
public sealed class TypeNamingRuleTests
{
	[Test]
	public async Task TypeNaming_GivenAnInterfaceWithoutThePrefix_ReportsIt()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type("OrderRepository", APITypeKind.Interface)
		);

		// Act
		var violations = evaluator.TypeNaming();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Rule).IsEqualTo(ArchitectureRules.TypeNaming);
		await Assert.That(violations[0].Message).Contains("does not start with 'I'");
	}

	[Test]
	public async Task TypeNaming_GivenAnInterfaceWithThePrefix_ReportsNothing()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type("IOrderRepository", APITypeKind.Interface)
		);

		// Act
		var violations = evaluator.TypeNaming();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task TypeNaming_GivenAnAttributeWithoutItsSuffix_ReportsIt()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type("Audited", baseType: "System.Attribute")
		);

		// Act
		var violations = evaluator.TypeNaming();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("does not end with 'Attribute'");
	}

	[Test]
	public async Task TypeNaming_GivenAnExceptionWithoutItsSuffix_ReportsIt()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type("OrderRejected", baseType: "System.Exception")
		);

		// Act
		var violations = evaluator.TypeNaming();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("does not end with 'Exception'");
	}

	[Test]
	public async Task TypeNaming_GivenADiscouragedSuffix_ReportsIt()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(TypeRuleFixture.Rules, TypeRuleFixture.Type("OrderManager"));

		// Act
		var violations = evaluator.TypeNaming();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("'Manager'");
	}

	[Test]
	public async Task TypeNaming_GivenTypeRulesOff_ReportsNothing()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			new TypeRules { Enabled = false },
			TypeRuleFixture.Type("OrderRepository", APITypeKind.Interface)
		);

		// Act
		var violations = evaluator.TypeNaming();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task TypeNaming_GivenAnUngovernedCategory_ReportsNothing()
	{
		// Arrange
		// Core is "shared"; the policy governs only "domain".
		TypeRules rules = new()
		{
			Enabled = true,
			AppliesTo = new TypeRuleScope { Categories = ["domain"] },
		};

		var evaluator = TypeRuleFixture.Evaluator(
			rules,
			TypeRuleFixture.Type("OrderRepository", APITypeKind.Interface)
		);

		// Act
		var violations = evaluator.TypeNaming();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task TypeNaming_GivenAGeneratedType_IsSkippedByDefault()
	{
		// Arrange
		APIType generated = new(
			"Core",
			"Purview.Core",
			"GeneratedManager",
			"Purview.Core.GeneratedManager",
			APITypeKind.Class,
			true,
			false,
			true,
			false,
			null,
			[],
			[],
			[new APIAttribute("System.CodeDom.Compiler.GeneratedCodeAttribute", "tool")]
		);

		var evaluator = TypeRuleFixture.Evaluator(TypeRuleFixture.Rules, generated);

		// Act
		var violations = evaluator.TypeNaming();

		// Assert
		await Assert.That(violations).IsEmpty();
	}
}
