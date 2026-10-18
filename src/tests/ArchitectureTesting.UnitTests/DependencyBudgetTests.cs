namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Exercises the dependency-budget ratchet.</summary>
public sealed class DependencyBudgetTests
{
	static DependencyBudget Budget(params DependencyBudgetEntry[] entries) =>
		new()
		{
			Enabled = true,
			FailOnStale = true,
			Entries = entries,
		};

	static DependencyBudgetEntry Entry(string project, params string[] references) =>
		new() { Project = project, References = references };

	static ArchitecturePolicy Policy(DependencyBudget budget) =>
		new()
		{
			Version = 3,
			Responsibilities =
			[
				new Responsibility
				{
					Name = "Core",
					Category = "shared",
					Purpose = "A responsibility declared by the budget fixture.",
					MayDependOn = ["Infra"],
					MustNotDependOn = [],
				},
				new Responsibility
				{
					Name = "Infra",
					Category = "shared",
					Purpose = "The project a budgeted reference points at.",
					MayDependOn = [],
					MustNotDependOn = [],
				},
			],
			DependencyBudget = budget,
		};

	static ArchitecturePolicyEvaluator Evaluator(DependencyBudget budget, string[]? coreReferences = null)
	{
		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
		{
			["Core"] = new("Core", "/repo/src/Core/Core.csproj", coreReferences ?? [], [], false),
			["Infra"] = new("Infra", "/repo/src/Infra/Infra.csproj", [], [], false),
		};

		return new ArchitecturePolicyEvaluator(Policy(budget), projects, new Dictionary<string, AssemblyNode>());
	}

	[Test]
	public async Task DependencyBudget_GivenAReferenceOutsideTheBudget_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(Budget(Entry("Core")), ["Infra"]);

		// Act
		var violations = evaluator.DependencyBudgetRule();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Rule).IsEqualTo(ArchitectureRules.DependencyBudgetRule);
		await Assert.That(violations[0].Message).Contains("not in its dependency budget");
	}

	[Test]
	public async Task DependencyBudget_GivenAReferenceInsideTheBudget_ReportsNothing()
	{
		// Arrange
		var evaluator = Evaluator(Budget(Entry("Core", "Infra")), ["Infra"]);

		// Act
		var violations = evaluator.DependencyBudgetRule();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task DependencyBudget_GivenAStaleAllowance_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(Budget(Entry("Core", "Infra")), []);

		// Act
		var violations = evaluator.DependencyBudgetRule();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("Tighten the budget");
	}

	[Test]
	public async Task DependencyBudget_GivenAStaleAllowanceAndFailOnStaleOff_ReportsNothing()
	{
		// Arrange
		DependencyBudget budget = new()
		{
			Enabled = true,
			FailOnStale = false,
			Entries = [Entry("Core", "Infra")],
		};

		var evaluator = Evaluator(budget, []);

		// Act
		var violations = evaluator.DependencyBudgetRule();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task DependencyBudget_GivenAnUnknownProject_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(Budget(Entry("Missing")), []);

		// Act
		var violations = evaluator.DependencyBudgetRule();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("not a production project");
	}

	[Test]
	public async Task DependencyBudget_GivenADuplicateEntry_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(Budget(Entry("Core"), Entry("Core")), []);

		// Act
		var violations = evaluator.DependencyBudgetRule();

		// Assert
		await Assert
			.That(violations.Any(violation => violation.Message.Contains("more than once", StringComparison.Ordinal)))
			.IsTrue();
	}

	[Test]
	public async Task DependencyBudget_GivenTheBudgetOff_ReportsNothing()
	{
		// Arrange
		var evaluator = Evaluator(new DependencyBudget { Enabled = false, Entries = [Entry("Core")] }, ["Infra"]);

		// Act
		var violations = evaluator.DependencyBudgetRule();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task DependencyBudget_IsNotWaivable()
	{
		// Arrange / Act
		var waivable = ArchitectureRules.IsWaivable(ArchitectureRules.DependencyBudgetRule);

		// Assert
		await Assert.That(waivable).IsFalse();
	}

	[Test]
	public async Task EnabledAdoptionRules_GivenTheBudgetOn_ListsTheRule()
	{
		// Arrange
		var evaluator = Evaluator(Budget(), []);

		// Act
		var rules = evaluator.EnabledAdoptionRules();

		// Assert
		await Assert.That(rules).Contains(ArchitectureRules.DependencyBudgetRule);
	}

	[Test]
	public async Task EnabledAdoptionRules_GivenTheBudgetOff_ListsNothing()
	{
		// Arrange
		var evaluator = Evaluator(new DependencyBudget { Enabled = false }, []);

		// Act
		var rules = evaluator.EnabledAdoptionRules();

		// Assert
		await Assert.That(rules).IsEmpty();
	}
}
