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

	/// <summary>
	/// The reported crash: a generic and a non-generic overload share a name and a parameter list,
	/// so a key built from those alone collides and the comparison throws before reporting anything.
	/// </summary>
	[Test]
	public async Task Compare_GivenAGenericAndNonGenericOverload_ReportsNothingRatherThanThrowing()
	{
		// Arrange
		APIMember[] overloads =
		[
			TypeRuleFixture.Member(
				"AsFailure",
				typeName: "Purview.Results.Result<!!0, Purview.Core.OrderError>",
				isStatic: true,
				parameters: ["Purview.Core.OrderNotFound"],
				genericArity: 1
			),
			TypeRuleFixture.Member(
				"AsFailure",
				typeName: "Purview.Results.Result<Purview.Core.OrderError>",
				isStatic: true,
				parameters: ["Purview.Core.OrderNotFound"]
			),
		];

		var before = Capture(TypeRuleFixture.Type("OrderErrors", members: overloads));

		// Act
		var changes = APIBaselineComparer.Compare(
			before,
			Capture(TypeRuleFixture.Type("OrderErrors", members: overloads))
		);

		// Assert
		await Assert.That(changes).IsEmpty();
	}

	/// <summary>Two members that differ only in return type are legal C# and must not collide.</summary>
	[Test]
	public async Task Compare_GivenOverloadsDifferingOnlyByReturnType_ReportsNothing()
	{
		// Arrange
		APIMember[] overloads =
		[
			TypeRuleFixture.Member("Convert", typeName: "System.Int32", parameters: ["System.String"]),
			TypeRuleFixture.Member("Convert", typeName: "System.Int64", parameters: ["System.String"]),
		];

		var before = Capture(TypeRuleFixture.Type("Converter", members: overloads));

		// Act
		var changes = APIBaselineComparer.Compare(
			before,
			Capture(TypeRuleFixture.Type("Converter", members: overloads))
		);

		// Assert
		await Assert.That(changes).IsEmpty();
	}

	/// <summary>
	/// The point of not throwing: a type carrying colliding overloads must still have real drift
	/// reported, both on the overloads themselves and on the rest of the surface.
	/// </summary>
	[Test]
	public async Task Compare_GivenCollidingOverloadsAndRealDrift_ReportsTheDrift()
	{
		// Arrange
		var generic = TypeRuleFixture.Member(
			"AsFailure",
			typeName: "Purview.Results.Result<!!0>",
			parameters: ["Purview.Core.OrderNotFound"],
			genericArity: 1
		);

		var plain = TypeRuleFixture.Member(
			"AsFailure",
			typeName: "Purview.Results.Result",
			parameters: ["Purview.Core.OrderNotFound"]
		);

		var before = Capture(TypeRuleFixture.Type("OrderErrors", members: [generic, plain]));

		// The generic overload is gone and an unrelated member has arrived.
		var after = Capture(TypeRuleFixture.Type("OrderErrors", members: [plain, TypeRuleFixture.Member("AsSuccess")]));

		// Act
		var changes = APIBaselineComparer.Compare(before, after);

		// Assert
		await Assert.That(changes.Count).IsEqualTo(2);

		await Assert
			.That(
				changes.Any(change =>
					change.Kind == APIBaselineChangeKind.Removed
					&& change.Subject.Contains("AsFailure<T>", StringComparison.Ordinal)
				)
			)
			.IsTrue()
			.Because("the generic overload was removed and must be named unambiguously");

		await Assert
			.That(
				changes.Any(change =>
					change.Kind == APIBaselineChangeKind.Added
					&& change.Subject.Contains("AsSuccess", StringComparison.Ordinal)
				)
			)
			.IsTrue()
			.Because("the new member must still be reported");
	}

	/// <summary>A single overload still reports a return-type change as a change, not a swap.</summary>
	[Test]
	public async Task Compare_GivenOneOverloadWhoseReturnTypeChanged_ReportsAChange()
	{
		// Arrange
		var before = Capture(
			TypeRuleFixture.Type(
				"Converter",
				members: [TypeRuleFixture.Member("Convert", typeName: "System.Int32", parameters: ["System.String"])]
			)
		);

		var after = Capture(
			TypeRuleFixture.Type(
				"Converter",
				members: [TypeRuleFixture.Member("Convert", typeName: "System.Int64", parameters: ["System.String"])]
			)
		);

		// Act
		var changes = APIBaselineComparer.Compare(before, after);

		// Assert
		await Assert.That(changes).HasSingleItem();
		await Assert.That(changes[0].Kind).IsEqualTo(APIBaselineChangeKind.Changed);
		await Assert.That(changes[0].Detail).Contains("System.Int64");
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
