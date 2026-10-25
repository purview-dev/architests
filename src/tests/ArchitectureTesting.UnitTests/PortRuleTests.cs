namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Exercises the port implementation and purity rules.</summary>
public sealed class PortRuleTests
{
	static PortRules Enabled => new() { Enabled = true };

	static ArchitecturePolicy Policy(PortRules ports, string coreCategory = "domain") =>
		new()
		{
			Version = 3,
			Responsibilities =
			[
				new Responsibility
				{
					Name = "Core",
					Category = coreCategory,
					Purpose = "The inner layer whose interfaces are ports.",
					MayDependOn = [],
					MustNotDependOn = [],
				},
				new Responsibility
				{
					Name = "Infra",
					Category = "infrastructure",
					Purpose = "The adapter layer that implements the ports.",
					MayDependOn = ["Core"],
					MustNotDependOn = [],
				},
			],
			PortRules = ports,
		};

	static ArchitecturePolicyEvaluator Evaluator(
		PortRules ports,
		APIType[] coreTypes,
		APIType[] infraTypes,
		string coreCategory = "domain"
	)
	{
		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
		{
			["Core"] = new("Core", "/repo/src/Core/Core.csproj", [], [], false) { RootNamespace = "Purview.Core" },
			["Infra"] = new("Infra", "/repo/src/Infra/Infra.csproj", [], [], false) { RootNamespace = "Purview.Infra" },
		};

		Dictionary<string, APIAssembly> surfaces = new(StringComparer.OrdinalIgnoreCase)
		{
			["Core"] = new("Core", "Core", [], coreTypes),
			["Infra"] = new("Infra", "Infra", [], infraTypes),
		};

		return new ArchitecturePolicyEvaluator(
			Policy(ports, coreCategory),
			projects,
			new Dictionary<string, AssemblyNode>(),
			apiSurfaces: surfaces
		);
	}

	static APIType Port(params APIMember[] members) =>
		TypeRuleFixture.Type("IOrderStore", APITypeKind.Interface, members: members);

	public static APIType Implementation =>
		TypeRuleFixture.Type(
			"SqlOrderStore",
			project: "Infra",
			ns: "Purview.Infra",
			interfaces: ["Purview.Core.IOrderStore"]
		);

	[Test]
	public async Task PortImplementation_GivenAPortWithNoImplementation_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(Enabled, [Port()], []);

		// Act
		var violations = evaluator.PortImplementation();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Rule).IsEqualTo(ArchitectureRules.PortImplementation);
		await Assert.That(violations[0].Message).Contains("no implementation");
	}

	[Test]
	public async Task PortImplementation_GivenAnImplementation_ReportsNothing()
	{
		// Arrange
		var evaluator = Evaluator(Enabled, [Port()], [Implementation]);

		// Act
		var violations = evaluator.PortImplementation();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	/// <summary>
	/// An adapter the composition root wires is commonly internal. The rule asks whether the
	/// boundary was crossed, not who can see the type that crosses it.
	/// </summary>
	[Test]
	public async Task PortImplementation_GivenAnInternalImplementation_ReportsNothing()
	{
		// Arrange
		var evaluator = Evaluator(Enabled, [Port()], [Implementation with { IsPublic = false }]);

		// Act
		var violations = evaluator.PortImplementation();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task PortImplementation_GivenAnInternalImplementationAndPublicRequired_ReportsIt()
	{
		// Arrange
		PortRules ports = new() { Enabled = true, RequirePublicImplementation = true };
		var evaluator = Evaluator(ports, [Port()], [Implementation with { IsPublic = false }]);

		// Act
		var violations = evaluator.PortImplementation();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("no public implementation");
	}

	[Test]
	public async Task PortImplementation_GivenAPortOutsideThePortCategories_ReportsNothing()
	{
		// Arrange
		// A shared interface is not a port, so an unimplemented one is not a finding.
		var evaluator = Evaluator(Enabled, [Port()], [], coreCategory: "shared");

		// Act
		var violations = evaluator.PortImplementation();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task PortPurity_GivenAnInfrastructureTypeInTheSignature_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(
			Enabled,
			[
				Port(
					TypeRuleFixture.Member("Context", APIMemberKind.Property, "Microsoft.EntityFrameworkCore.DbContext")
				),
			],
			[Implementation]
		);

		// Act
		var violations = evaluator.PortPurity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Rule).IsEqualTo(ArchitectureRules.PortPurity);
		await Assert.That(violations[0].Message).Contains("Microsoft.EntityFrameworkCore");
	}

	/// <summary>
	/// The setting is named and documented as a list of prefixes. A substring match also flagged a
	/// type whose namespace merely contained the prefix part-way through.
	/// </summary>
	[Test]
	public async Task PortPurity_GivenAPrefixAppearingMidNamespace_ReportsNothing()
	{
		// Arrange
		// 'Contoso.System.DataPoint' is the repository's own type; it only looks like System.Data.
		var evaluator = Evaluator(
			Enabled,
			[Port(TypeRuleFixture.Member("Reading", APIMemberKind.Property, "Contoso.System.DataPoint"))],
			[Implementation]
		);

		// Act
		var violations = evaluator.PortPurity();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	/// <summary>A technology hidden inside a generic argument is exposed just as plainly.</summary>
	[Test]
	public async Task PortPurity_GivenATechnologyInAGenericArgument_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(
			Enabled,
			[
				Port(
					TypeRuleFixture.Member(
						"Connections",
						APIMemberKind.Property,
						"System.Collections.Generic.IReadOnlyList<System.Data.IDbConnection>"
					)
				),
			],
			[Implementation]
		);

		// Act
		var violations = evaluator.PortPurity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("System.Data");
	}

	[Test]
	public async Task PortPurity_GivenATechnologyNeutralSignature_ReportsNothing()
	{
		// Arrange
		var evaluator = Evaluator(
			Enabled,
			[
				Port(
					TypeRuleFixture.Member(
						"Orders",
						APIMemberKind.Property,
						"System.Collections.Generic.IReadOnlyList<Purview.Core.Order>"
					)
				),
			],
			[Implementation]
		);

		// Act
		var violations = evaluator.PortPurity();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task EnabledPortRules_GivenTheGroupOn_ListsBothRules()
	{
		// Arrange
		var evaluator = Evaluator(Enabled, [], []);

		// Act
		var rules = evaluator.EnabledPortRules();

		// Assert
		await Assert.That(rules).Contains(ArchitectureRules.PortImplementation);
		await Assert.That(rules).Contains(ArchitectureRules.PortPurity);
	}

	[Test]
	public async Task EnabledPortRules_GivenTheGroupOff_ListsNothing()
	{
		// Arrange
		var evaluator = Evaluator(new PortRules { Enabled = false }, [], []);

		// Act
		var rules = evaluator.EnabledPortRules();

		// Assert
		await Assert.That(rules).IsEmpty();
	}
}
