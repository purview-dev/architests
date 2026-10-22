namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Exercises the attribute rule against in-memory surfaces.</summary>
public sealed class AttributePolicyRuleTests
{
	[Test]
	public async Task AttributePolicy_GivenAMissingRequiredAttribute_ReportsIt()
	{
		// Arrange
		TypeRules rules = new() { Enabled = true, RequiredAttributes = ["Contoso.AuditedAttribute"] };

		var evaluator = TypeRuleFixture.Evaluator(rules, TypeRuleFixture.Type("Order"));

		// Act
		var violations = evaluator.AttributePolicy();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Rule).IsEqualTo(ArchitectureRules.AttributePolicy);
		await Assert.That(violations[0].Message).Contains("missing the required attribute");
	}

	[Test]
	public async Task AttributePolicy_GivenAPresentRequiredAttribute_ReportsNothing()
	{
		// Arrange
		TypeRules rules = new() { Enabled = true, RequiredAttributes = ["Contoso.AuditedAttribute"] };

		var evaluator = TypeRuleFixture.Evaluator(
			rules,
			TypeRuleFixture.Type("Order", attributes: [new APIAttribute("Contoso.AuditedAttribute", null)])
		);

		// Act
		var violations = evaluator.AttributePolicy();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task AttributePolicy_GivenAForbiddenAttribute_ReportsIt()
	{
		// Arrange
		TypeRules rules = new() { Enabled = true, ForbiddenAttributes = ["System.SerializableAttribute"] };

		var evaluator = TypeRuleFixture.Evaluator(
			rules,
			TypeRuleFixture.Type("Order", attributes: [new APIAttribute("System.SerializableAttribute", null)])
		);

		// Act
		var violations = evaluator.AttributePolicy();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("forbidden attribute");
	}

	[Test]
	public async Task AttributePolicy_GivenObsoleteWithoutAMessage_ReportsIt()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type("Order", attributes: [new APIAttribute("System.ObsoleteAttribute", null)])
		);

		// Act
		var violations = evaluator.AttributePolicy();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("[Obsolete] without a message");
	}

	[Test]
	public async Task AttributePolicy_GivenObsoleteWithAMessage_ReportsNothing()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type(
				"Order",
				attributes: [new APIAttribute("System.ObsoleteAttribute", "Use PurchaseOrder instead.")]
			)
		);

		// Act
		var violations = evaluator.AttributePolicy();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task AttributePolicy_GivenObsoleteAllowedWithoutAMessage_ReportsNothing()
	{
		// Arrange
		TypeRules rules = new() { Enabled = true, AllowObsoleteWithoutMessage = true };

		var evaluator = TypeRuleFixture.Evaluator(
			rules,
			TypeRuleFixture.Type("Order", attributes: [new APIAttribute("System.ObsoleteAttribute", null)])
		);

		// Act
		var violations = evaluator.AttributePolicy();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task AttributePolicy_GivenAnObsoleteMemberWithoutAMessage_ReportsIt()
	{
		// Arrange
		var evaluator = TypeRuleFixture.Evaluator(
			TypeRuleFixture.Rules,
			TypeRuleFixture.Type(
				"Order",
				members:
				[
					TypeRuleFixture.Member("Cancel", attributes: [new APIAttribute("System.ObsoleteAttribute", null)]),
				]
			)
		);

		// Act
		var violations = evaluator.AttributePolicy();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("member 'Purview.Core.Order.Cancel'");
	}
}
