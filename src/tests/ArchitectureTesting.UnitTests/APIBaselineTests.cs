namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Exercises baseline capture and canonical JSON.</summary>
public sealed class APIBaselineTests
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

	static APIAssembly Assembly(params APIType[] types) => new("Core", "Core", [], types);

	[Test]
	public async Task Capture_GivenInternalAndPublicTypes_RecordsOnlyThePublicSurface()
	{
		// Arrange
		Dictionary<string, APIAssembly> surfaces = new(StringComparer.OrdinalIgnoreCase)
		{
			["Core"] = Assembly(
				TypeRuleFixture.Type("Order"),
				TypeRuleFixture.Type("OrderRepository", isPublic: false)
			),
		};

		// Act
		var baseline = APIBaseline.Capture(surfaces, Policy);

		// Assert
		await Assert.That(baseline.Projects).HasSingleItem();
		await Assert.That(baseline.Projects[0].Types).HasSingleItem();
		await Assert.That(baseline.Projects[0].Types[0].Name).IsEqualTo("Purview.Core.Order");
		await Assert.That(baseline.Projects[0].Responsibility).IsEqualTo("Core");
	}

	[Test]
	public async Task ToCanonicalJson_GivenReorderedInput_IsStable()
	{
		// Arrange
		var first = APIBaseline.Capture(
			new Dictionary<string, APIAssembly>(StringComparer.OrdinalIgnoreCase)
			{
				["Core"] = Assembly(TypeRuleFixture.Type("Order"), TypeRuleFixture.Type("Customer")),
			},
			Policy
		);

		var second = APIBaseline.Capture(
			new Dictionary<string, APIAssembly>(StringComparer.OrdinalIgnoreCase)
			{
				["Core"] = Assembly(TypeRuleFixture.Type("Customer"), TypeRuleFixture.Type("Order")),
			},
			Policy
		);

		// Act
		var canonical = first.ToCanonicalJson();

		// Assert
		await Assert.That(canonical).IsEqualTo(second.ToCanonicalJson());
		await Assert.That(canonical.EndsWith('\n')).IsTrue();
		await Assert.That(canonical).DoesNotContain("\r");
	}

	[Test]
	public async Task WriteAndRead_GivenABaseline_RoundTripsTheSameDocument()
	{
		// Arrange
		var baseline = APIBaseline.Capture(
			new Dictionary<string, APIAssembly>(StringComparer.OrdinalIgnoreCase)
			{
				["Core"] = Assembly(
					TypeRuleFixture.Type("Order", members: [TypeRuleFixture.Member("Cancel", typeName: "System.Void")])
				),
			},
			Policy
		);

		var path = Path.Combine(Path.GetTempPath(), $"PurviewBaseline{Guid.NewGuid():N}.json");

		try
		{
			// Act
			baseline.Write(path);
			var read = APIBaseline.Read(path);

			// Assert
			await Assert.That(read.ToCanonicalJson()).IsEqualTo(baseline.ToCanonicalJson());
			await Assert.That(read.Projects[0].Types[0].Members).HasSingleItem();
		}
		finally
		{
			File.Delete(path);
		}
	}

	[Test]
	public async Task Capture_GivenAMethod_RecordsItsParameterTypes()
	{
		// Arrange
		var method = TypeRuleFixture.Member("Cancel", typeName: "System.Boolean");
		method = method with { Parameters = ["System.String", "System.Int32"] };

		Dictionary<string, APIAssembly> surfaces = new(StringComparer.OrdinalIgnoreCase)
		{
			["Core"] = Assembly(TypeRuleFixture.Type("Order", members: [method])),
		};

		// Act
		var baseline = APIBaseline.Capture(surfaces, Policy);

		// Assert
		var member = baseline.Projects[0].Types[0].Members.Single();
		await Assert.That(member.Parameters).IsEquivalentTo(["System.String", "System.Int32"]);
	}
}
