using System.Reflection;

namespace Purview.Architecture.Testing.UnitTests;

/// <summary>
/// Guards the rule catalogue, which is the single authority the documentation and waivers use.
/// </summary>
public sealed class RuleCatalogueTests
{
	[Test]
	public async Task Catalogue_GivenItsIdentifiers_HasNoDuplicates()
	{
		// Arrange
		// A duplicate would make a waiver match one rule and the docs report another.
		var identifiers = ArchitectureRules.All;

		// Act
		var distinct = identifiers.Distinct(StringComparer.OrdinalIgnoreCase).Count();

		// Assert
		await Assert.That(distinct).IsEqualTo(identifiers.Count);
	}

	[Test]
	public async Task Catalogue_GivenThePublicConstants_DescribesEachExactlyOnce()
	{
		// Arrange
		// A constant without a descriptor is a rule no document test can see; a descriptor without
		// a constant is a rule no code can name.
		var constants = typeof(ArchitectureRules)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(field => field.IsLiteral && field.FieldType == typeof(string))
			.Select(field => (string)field.GetRawConstantValue()!)
			.ToArray();

		// Act
		var missing = constants.Except(ArchitectureRules.All, StringComparer.OrdinalIgnoreCase).ToArray();
		var extra = ArchitectureRules.All.Except(constants, StringComparer.OrdinalIgnoreCase).ToArray();

		// Assert
		await Assert.That(missing).IsEmpty().Because($"undescribed rules: {string.Join(", ", missing)}");
		await Assert.That(extra).IsEmpty().Because($"described but unnameable rules: {string.Join(", ", extra)}");
	}

	[Test]
	public async Task Catalogue_GivenThePolicyGuards_MarksThemUnwaivable()
	{
		// Arrange
		string[] guards =
		[
			ArchitectureRules.PolicyIntegrity,
			ArchitectureRules.GraphDiscovery,
			ArchitectureRules.WaiverIntegrity,
		];

		// Act
		var waivable = guards.Where(ArchitectureRules.IsWaivable).ToArray();

		// Assert
		await Assert
			.That(waivable)
			.IsEmpty()
			.Because($"a waiver excusing a guard would make the policy unfalsifiable: {string.Join(", ", waivable)}");
	}

	[Test]
	public async Task Catalogue_GivenAnOrdinaryRule_MarksItWaivable()
	{
		// Arrange / Act
		var waivable = ArchitectureRules.IsWaivable(ArchitectureRules.DependencyMatrix);

		// Assert
		await Assert.That(waivable).IsTrue();
	}

	[Test]
	public async Task Catalogue_GivenAnUnknownRule_TreatsItAsWaivable()
	{
		// Arrange
		// An additional rule is waivable unless it declares otherwise, and it has no descriptor here.
		var waivable = ArchitectureRules.IsWaivable("noConsoleLogging");

		// Assert
		await Assert.That(waivable).IsTrue();
	}
}
