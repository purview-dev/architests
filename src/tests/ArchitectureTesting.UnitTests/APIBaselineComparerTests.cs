namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Exercises the baseline comparison.</summary>
public sealed class APIBaselineComparerTests
{
	static ArchitecturePolicy Policy =>
		new()
		{
			Version = 3,
			Responsibilities =
			[
				new Responsibility
				{
					Name = "Core",
					Category = "domain",
					Purpose = "A responsibility declared by the baseline fixture.",
					MayDependOn = [],
					MustNotDependOn = [],
				},
			],
		};

	static APIBaseline Capture(params APIType[] types) =>
		APIBaseline.Capture(
			new Dictionary<string, APIAssembly>(StringComparer.OrdinalIgnoreCase)
			{
				["Core"] = new("Core", "Core", [], types),
			},
			Policy
		);

	[Test]
	public async Task Compare_GivenIdenticalBaselines_ReportsNothing()
	{
		// Arrange
		var baseline = Capture(TypeRuleFixture.Type("Order"));

		// Act
		var changes = APIBaselineComparer.Compare(baseline, Capture(TypeRuleFixture.Type("Order")));

		// Assert
		await Assert.That(changes).IsEmpty();
	}

	[Test]
	public async Task Compare_GivenAnAddedType_ReportsAnAddition()
	{
		// Arrange
		var baseline = Capture(TypeRuleFixture.Type("Order"));

		// Act
		var changes = APIBaselineComparer.Compare(
			baseline,
			Capture(TypeRuleFixture.Type("Order"), TypeRuleFixture.Type("Customer"))
		);

		// Assert
		await Assert.That(changes).HasSingleItem();
		await Assert.That(changes[0].Kind).IsEqualTo(APIBaselineChangeKind.Added);
		await Assert.That(changes[0].Subject).IsEqualTo("Purview.Core.Customer");
	}

	[Test]
	public async Task Compare_GivenARemovedMember_ReportsItAsBreaking()
	{
		// Arrange
		var before = Capture(TypeRuleFixture.Type("Order", members: [TypeRuleFixture.Member("Cancel")]));

		// Act
		var changes = APIBaselineComparer.Compare(before, Capture(TypeRuleFixture.Type("Order")));

		// Assert
		await Assert.That(changes).HasSingleItem();
		await Assert.That(changes[0].Kind).IsEqualTo(APIBaselineChangeKind.Removed);
		await Assert.That(changes[0].Detail).Contains("breaking change");
	}

	[Test]
	public async Task Compare_GivenAChangedMemberType_ReportsTheChange()
	{
		// Arrange
		var before = Capture(
			TypeRuleFixture.Type(
				"Order",
				members: [TypeRuleFixture.Member("Status", APIMemberKind.Property, "System.String")]
			)
		);

		var after = Capture(
			TypeRuleFixture.Type(
				"Order",
				members: [TypeRuleFixture.Member("Status", APIMemberKind.Property, "System.Int32")]
			)
		);

		// Act
		var changes = APIBaselineComparer.Compare(before, after);

		// Assert
		await Assert.That(changes).HasSingleItem();
		await Assert.That(changes[0].Kind).IsEqualTo(APIBaselineChangeKind.Changed);
		await Assert.That(changes[0].Detail).Contains("System.String");
		await Assert.That(changes[0].Detail).Contains("System.Int32");
	}

	[Test]
	public async Task Compare_GivenAChangedTypeShape_ReportsTheShapeChange()
	{
		// Arrange
		var before = Capture(TypeRuleFixture.Type("Order", isSealed: true));

		// Act
		var changes = APIBaselineComparer.Compare(before, Capture(TypeRuleFixture.Type("Order", isSealed: false)));

		// Assert
		await Assert.That(changes).HasSingleItem();
		await Assert.That(changes[0].Detail).Contains("sealed changed from True to False");
	}
}
