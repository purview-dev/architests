namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Exercises the type-namespace rule against in-memory surfaces.</summary>
public sealed class TypeNamespaceRuleTests
{
	[Test]
	public async Task TypeNamespace_GivenATypeOutsideTheRootNamespace_ReportsIt()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type("Order", ns: "Contoso.Legacy")
		);

		// Act
		var violations = evaluator.TypeNamespace();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Rule).IsEqualTo(ArchitectureRules.TypeNamespace);
		await Assert.That(violations[0].Message).Contains("outside the project's root namespace");
	}

	[Test]
	public async Task TypeNamespace_GivenASubNamespace_ReportsNothing()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type("Order", ns: "Purview.Core.Model")
		);

		// Act
		var violations = evaluator.TypeNamespace();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task TypeNamespace_GivenTheRootNamespace_ReportsNothing()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type("Order", ns: "Purview.Core")
		);

		// Act
		var violations = evaluator.TypeNamespace();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task TypeNamespace_GivenAPrefixThatOnlyLooksSimilar_ReportsIt()
	{
		// Arrange
		// 'Purview.CoreX' is not a sub-namespace of 'Purview.Core'.
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type("Order", ns: "Purview.CoreX")
		);

		// Act
		var violations = evaluator.TypeNamespace();

		// Assert
		await Assert.That(violations).HasSingleItem();
	}
}
