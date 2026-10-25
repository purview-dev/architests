namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Exercises the type-shape rule against in-memory surfaces.</summary>
public sealed class TypeShapeRuleTests
{
	[Test]
	public async Task TypeShape_GivenAnUnsealedPublicClass_ReportsIt()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type("OrderService", isSealed: false)
		);

		// Act
		var violations = evaluator.TypeShape();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Rule).IsEqualTo(ArchitectureRules.TypeShape);
		await Assert.That(violations[0].Message).Contains("Seal it");
	}

	/// <summary>
	/// The container a C# extension block emits looks exactly like a utility class that forgot to be
	/// static, but no author can declare it static: it does not exist in source.
	/// </summary>
	[Test]
	public async Task TypeShape_GivenACompilerSynthesizedContainer_ReportsNothing()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type(
				"<G>$7A6AE160A7649B5FD624AE28A23FFA64",
				ns: string.Empty,
				fullName: "Ext.<G>$7A6AE160A7649B5FD624AE28A23FFA64`1",
				isNested: true,
				isSealed: false,
				members: [TypeRuleFixture.Member("DoubledLength", typeName: "System.Int32", isStatic: true)]
			)
		);

		// Act
		var violations = evaluator.TypeShape();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	/// <summary>
	/// The synthesized container is skipped, but a hand-written utility class beside it is not: the
	/// fix must not blunt the rule.
	/// </summary>
	[Test]
	public async Task TypeShape_GivenASynthesizedContainerBesideARealViolation_ReportsOnlyTheRealOne()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type(
				"<G>$7A6AE160A7649B5FD624AE28A23FFA64",
				ns: string.Empty,
				fullName: "Ext.<G>$7A6AE160A7649B5FD624AE28A23FFA64`1",
				isNested: true,
				isSealed: false
			),
			TypeRuleFixture.Type(
				"OrderMaths",
				members: [TypeRuleFixture.Member("Total", typeName: "System.Int32", isStatic: true)]
			)
		);

		// Act
		var violations = evaluator.TypeShape();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("OrderMaths");
	}

	[Test]
	public async Task TypeShape_GivenAnAbstractClass_ReportsNothing()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type("OrderServiceBase", isAbstract: true, isSealed: false)
		);

		// Act
		var violations = evaluator.TypeShape();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task TypeShape_GivenAStaticClass_ReportsNothing()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type("OrderRules", isAbstract: true, isSealed: true)
		);

		// Act
		var violations = evaluator.TypeShape();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task TypeShape_GivenAPublicInstanceField_ReportsIt()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type(
				"Order",
				members: [TypeRuleFixture.Member("Status", APIMemberKind.Field, "System.String")]
			)
		);

		// Act
		var violations = evaluator.TypeShape();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("public field 'Status'");
	}

	[Test]
	public async Task TypeShape_GivenAStaticField_ReportsNothing()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type(
				"OrderRules",
				isAbstract: true,
				isSealed: true,
				members: [TypeRuleFixture.Member("Empty", APIMemberKind.Field, "System.String", isStatic: true)]
			)
		);

		// Act
		var violations = evaluator.TypeShape();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task TypeShape_GivenAMutableCollectionProperty_ReportsIt()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type(
				"Order",
				members:
				[
					TypeRuleFixture.Member(
						"Lines",
						APIMemberKind.Property,
						"System.Collections.Generic.List<Purview.Core.OrderLine>"
					),
				]
			)
		);

		// Act
		var violations = evaluator.TypeShape();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("IReadOnlyList<T>");
	}

	[Test]
	public async Task TypeShape_GivenAReadOnlyCollectionProperty_ReportsNothing()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type(
				"Order",
				members:
				[
					TypeRuleFixture.Member(
						"Lines",
						APIMemberKind.Property,
						"System.Collections.Generic.IReadOnlyList<Purview.Core.OrderLine>"
					),
				]
			)
		);

		// Act
		var violations = evaluator.TypeShape();

		// Assert
		await Assert.That(violations).IsEmpty();
	}
}
