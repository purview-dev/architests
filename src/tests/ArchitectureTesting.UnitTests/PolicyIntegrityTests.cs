namespace Purview.Architecture.Testing.UnitTests;

/// <summary>
/// Exercises the policy's own consistency checks against in-memory policies.
/// </summary>
/// <remarks>
/// These are the failures a schema cannot express: a name that does not exist, a dependency that is
/// both permitted and forbidden, and a prohibition the rest of the policy can bypass. They are
/// policy bugs rather than architectural choices, so the rule is always on and cannot be waived.
/// </remarks>
public sealed class PolicyIntegrityTests
{
	static Responsibility Responsibility(
		string name,
		string[]? may = null,
		string[]? mustNot = null,
		string[]? aliases = null
	) =>
		new()
		{
			Name = name,
			Aliases = aliases ?? [],
			Category = "shared",
			Purpose = "A responsibility declared by the policy-integrity fixture.",
			MayDependOn = may ?? [],
			MustNotDependOn = mustNot ?? [],
		};

	static ArchitecturePolicyEvaluator Evaluator(ArchitecturePolicy policy) =>
		new(policy, new Dictionary<string, ProjectNode>());

	static ArchitecturePolicy Policy(params Responsibility[] responsibilities) =>
		new() { Version = 3, Responsibilities = responsibilities };

	[Test]
	public async Task PolicyIntegrity_GivenAnUnknownMayDependOnTarget_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(Policy(Responsibility("Core", may: ["Missing"])));

		// Act
		var violations = evaluator.PolicyIntegrity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Rule).IsEqualTo(ArchitectureRules.PolicyIntegrity);
		await Assert.That(violations[0].Project).IsEqualTo("(policy)");
		await Assert.That(violations[0].Message).Contains("mayDependOn names 'Missing'");
	}

	[Test]
	public async Task PolicyIntegrity_GivenAnUnknownMustNotDependOnTarget_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(Policy(Responsibility("Core", mustNot: ["Missing"])));

		// Act
		var violations = evaluator.PolicyIntegrity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("mustNotDependOn names 'Missing'");
	}

	[Test]
	public async Task PolicyIntegrity_GivenADependencyInBothLists_ReportsTheOverlap()
	{
		// Arrange
		// A dependency cannot be both permitted and forbidden.
		var evaluator = Evaluator(
			Policy(
				Responsibility("Core", may: ["Persistence"], mustNot: ["Persistence"]),
				Responsibility("Persistence")
			)
		);

		// Act
		var violations = evaluator.PolicyIntegrity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("both permits and forbids 'Persistence'");
	}

	[Test]
	public async Task PolicyIntegrity_GivenADuplicateResponsibilityName_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(Policy(Responsibility("Core"), Responsibility("core")));

		// Act
		var violations = evaluator.PolicyIntegrity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("more than once");
	}

	[Test]
	public async Task PolicyIntegrity_GivenAnAliasThatIsAnotherName_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(
			Policy(Responsibility("Core", aliases: ["Persistence"]), Responsibility("Persistence"))
		);

		// Act
		var violations = evaluator.PolicyIntegrity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("which is the responsibility name 'Persistence'");
	}

	[Test]
	public async Task PolicyIntegrity_GivenTheSameAliasTwice_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(
			Policy(Responsibility("Core", aliases: ["Domain"]), Responsibility("Model", aliases: ["Domain"]))
		);

		// Act
		var violations = evaluator.PolicyIntegrity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("declared by both 'Core' and 'Model'");
	}

	[Test]
	public async Task PolicyIntegrity_GivenAReachableProhibition_ReportsThePath()
	{
		// Arrange
		// Core may use Application, Application may use Persistence, so Core can reach Persistence.
		var policy = Policy(
			Responsibility("Core", may: ["Application"], mustNot: ["Persistence"]),
			Responsibility("Application", may: ["Persistence"]),
			Responsibility("Persistence")
		);

		policy = new ArchitecturePolicy
		{
			Version = policy.Version,
			Responsibilities = policy.Responsibilities,
			PolicyIntegrity = new PolicyIntegritySettings { CheckReachability = true },
		};

		var evaluator = Evaluator(policy);

		// Act
		var violations = evaluator.PolicyIntegrity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("Core -> Application -> Persistence");
	}

	[Test]
	public async Task PolicyIntegrity_GivenAReachableProhibitionWithoutTheOptIn_ReportsNothing()
	{
		// Arrange
		// The direct lists are consistent; only reachability can see the bypass, and it is opt-in.
		var evaluator = Evaluator(
			Policy(
				Responsibility("Core", may: ["Application"], mustNot: ["Persistence"]),
				Responsibility("Application", may: ["Persistence"]),
				Responsibility("Persistence")
			)
		);

		// Act
		var violations = evaluator.PolicyIntegrity();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task PolicyIntegrity_GivenAnUnknownForbiddenPackageScope_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(
			Policy(
				new Responsibility
				{
					Name = "Core",
					Category = "shared",
					Purpose = "A responsibility declared by the policy-integrity fixture.",
					MayDependOn = [],
					MustNotDependOn = [],
					ForbiddenPackageScope = "everything",
				}
			)
		);

		// Act
		var violations = evaluator.PolicyIntegrity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("not a scope");
	}

	[Test]
	public async Task PolicyIntegrity_GivenAConsistentPolicy_ReportsNothing()
	{
		// Arrange
		var evaluator = Evaluator(
			Policy(Responsibility("Core", mustNot: ["Application"]), Responsibility("Application", may: ["Core"]))
		);

		// Act
		var violations = evaluator.PolicyIntegrity();

		// Assert
		await Assert.That(violations).IsEmpty();
	}
}
