namespace Purview.Architecture.Testing.UnitTests;

/// <summary>
/// Exercises the type-level and framework rules, and the waiver and extension points, against graphs
/// built in memory.
/// </summary>
/// <remarks>
/// The evaluator takes an in-memory project and assembly graph, which is what lets a type-level rule
/// be stated as a three-project fixture rather than a compiled repository.
/// </remarks>
public sealed class AssemblyRuleTests
{
	static ProjectNode Project(
		string name,
		IReadOnlyList<string>? references = null,
		IReadOnlyList<string>? packages = null,
		bool isTest = false,
		IReadOnlyList<string>? frameworks = null
	) =>
		new(name, $"/repo/src/{name}/{name}.csproj", references ?? [], packages ?? [], isTest)
		{
			FrameworkReferences = frameworks ?? [],
		};

	static AssemblyNode Assembly(string project, params (string Target, string Type)[] references) =>
		new(
			project,
			$"Purview.Test.{project}",
			[.. references.Select(reference => new AssemblyReference(reference.Target, reference.Type))]
		);

	static ArchitecturePolicy Policy(params Responsibility[] responsibilities) =>
		new()
		{
			Version = 2,
			Responsibilities = responsibilities,
			AssemblyRules = new AssemblyRules { Enabled = true },
		};

	static ArchitecturePolicyEvaluator Evaluator(
		ArchitecturePolicy policy,
		ProjectNode[] projects,
		AssemblyNode[] assemblies,
		IReadOnlyList<IArchitectureRule>? additionalRules = null
	) =>
		new(
			policy,
			projects.ToDictionary(project => project.Name, StringComparer.OrdinalIgnoreCase),
			assemblies.ToDictionary(assembly => assembly.ProjectName, StringComparer.OrdinalIgnoreCase),
			additionalRules
		);

	static Responsibility Core =>
		new()
		{
			Name = "Core",
			Category = "domain",
			Purpose = "The fundamental domain model and its rules.",
			MayDependOn = ["Shared"],
			MustNotDependOn = ["Persistence", "Composition"],
			ForbiddenFrameworkReferences = ["Microsoft.AspNetCore.App"],
		};

	static Responsibility Application =>
		new()
		{
			Name = "Application",
			Category = "application",
			Purpose = "Coordinates use cases and workflows.",
			MayDependOn = ["Core"],
			MustNotDependOn = ["Persistence", "Composition"],
		};

	static Responsibility Persistence =>
		new()
		{
			Name = "Persistence",
			Category = "infrastructure",
			Purpose = "Durable data access.",
			MayDependOn = ["Core", "Application"],
			MustNotDependOn = ["Composition"],
			SiblingIndependence = true,
		};

	static Responsibility Shared =>
		new()
		{
			Name = "Shared",
			Category = "shared",
			Purpose = "Small, broadly reusable, domain-independent utilities.",
			MayDependOn = [],
			MustNotDependOn = [],
		};

	static Responsibility Wiring =>
		new()
		{
			Name = "Wiring",
			Category = "composition",
			Purpose = "The composition root that selects concrete implementations.",
			MayDependOn = ["Core", "Persistence"],
			MustNotDependOn = [],
		};

	static Responsibility API =>
		new()
		{
			Name = "API",
			Category = "host",
			Purpose = "The HTTP host that exposes the application.",
			MayDependOn = ["Wiring"],
			MustNotDependOn = [],
		};

	[Test]
	public async Task AssemblyDependencyMatrix_GivenAForbiddenTypeReference_ReportsItWithTheCorrection()
	{
		// Arrange
		var evaluator = Evaluator(
			Policy(Core, Persistence),
			[Project("Core"), Project("Persistence")],
			[Assembly("Core", ("Persistence", "Purview.Persistence.Store")), Assembly("Persistence")]
		);

		// Act
		var violations = evaluator.AssemblyDependencyMatrix();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Rule).IsEqualTo(ArchitectureRules.AssemblyDependencyMatrix);
		await Assert.That(violations[0].Project).IsEqualTo("Core");
		await Assert.That(violations[0].Message).Contains("Purview.Persistence.Store");
		await Assert.That(violations[0].Message).Contains("must not depend on Persistence");
		await Assert.That(violations[0].Message).Contains("Invert it");
	}

	[Test]
	public async Task AssemblyDependencyMatrix_GivenAnIndirectReference_ReportsWhatTheProjectGraphCannot()
	{
		// Arrange
		// Core declares no ProjectReference to Persistence; the dependency only exists at type level.
		var evaluator = Evaluator(
			Policy(Core, Persistence),
			[Project("Core"), Project("Persistence")],
			[Assembly("Core", ("Persistence", "Purview.Persistence.Store")), Assembly("Persistence")]
		);

		// Act
		var projectLevel = evaluator.DependencyMatrix();
		var typeLevel = evaluator.AssemblyDependencyMatrix();

		// Assert
		await Assert.That(projectLevel).IsEmpty();
		await Assert.That(typeLevel).HasSingleItem();
	}

	[Test]
	public async Task AssemblyDependencyMatrix_GivenAPermittedReference_ReportsNothing()
	{
		// Arrange
		var evaluator = Evaluator(
			Policy(Core, Shared),
			[Project("Core"), Project("Shared")],
			[Assembly("Core", ("Shared", "Purview.Shared.Utility")), Assembly("Shared")]
		);

		// Act
		var violations = evaluator.AssemblyDependencyMatrix();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task AssemblyDependencyMatrix_GivenAReferenceNeitherPermittedNorForbidden_ReportsIt()
	{
		// Arrange
		// Silence on a type-level dependency is not permission; an unlisted edge is a gap in the policy.
		var evaluator = Evaluator(
			Policy(Core, Application, Persistence),
			[Project("Core"), Project("Application")],
			[Assembly("Core", ("Application", "Purview.Application.Handler")), Assembly("Application")]
		);

		// Act
		var violations = evaluator.AssemblyDependencyMatrix();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("neither permits nor forbids");
	}

	[Test]
	public async Task AssemblyDependencyMatrix_GivenASameResponsibilityReference_AllowsIt()
	{
		// Arrange
		// A qualified implementation is the same box on the diagram.
		var evaluator = Evaluator(
			Policy(Core),
			[Project("Core"), Project("Core.Sql")],
			[Assembly("Core", ("Core.Sql", "Purview.Core.Sql.Store")), Assembly("Core.Sql")]
		);

		// Act
		var violations = evaluator.AssemblyDependencyMatrix();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task AssemblyDependencyMatrix_GivenTheRuleIsDisabled_ReportsNothing()
	{
		// Arrange
		ArchitecturePolicy policy = new()
		{
			Version = 2,
			Responsibilities = [Core, Persistence],
			AssemblyRules = new AssemblyRules { Enabled = false },
		};

		var evaluator = Evaluator(
			policy,
			[Project("Core"), Project("Persistence")],
			[Assembly("Core", ("Persistence", "Purview.Persistence.Store")), Assembly("Persistence")]
		);

		// Act
		var violations = evaluator.AssemblyDependencyMatrix();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task AssemblyDependencyMatrix_GivenNoAssemblies_ReportsADiscoveryFailure()
	{
		// Arrange
		// Enabled with nothing found must be loud, not a green run that checked nothing.
		var evaluator = Evaluator(Policy(Core), [Project("Core")], []);

		// Act
		var violations = evaluator.AssemblyDependencyMatrix();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("No compiled assemblies");
	}

	[Test]
	public async Task ForbiddenFrameworkReferences_GivenAMatchingPrefix_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(Policy(Core), [Project("Core", frameworks: ["Microsoft.AspNetCore.App"])], []);

		// Act
		var violations = evaluator.ForbiddenFrameworkReferences();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Rule).IsEqualTo(ArchitectureRules.ForbiddenFramework);
		await Assert.That(violations[0].Message).Contains("Microsoft.AspNetCore.App");
	}

	[Test]
	public async Task ForbiddenFrameworkReferences_GivenAnUnrelatedFramework_AllowsIt()
	{
		// Arrange
		var evaluator = Evaluator(Policy(Core), [Project("Core", frameworks: ["Microsoft.NETCore.App"])], []);

		// Act
		var violations = evaluator.ForbiddenFrameworkReferences();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task CompositionBoundary_GivenACompositionCategoryProject_ReportsIt()
	{
		// Arrange
		// The rule keys on category, not on the literal name 'Composition'.
		var evaluator = Evaluator(
			Policy(Persistence, Wiring),
			[Project("Persistence", ["Wiring"]), Project("Wiring")],
			[]
		);

		// Act
		var violations = evaluator.CompositionBoundary();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Project).IsEqualTo("Persistence");
	}

	[Test]
	public async Task CompositionBoundary_GivenAHostProject_AllowsIt()
	{
		// Arrange
		var evaluator = Evaluator(Policy(API, Wiring), [Project("API", ["Wiring"]), Project("Wiring")], []);

		// Act
		var violations = evaluator.CompositionBoundary();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task SiblingIndependence_GivenAQualifiedSiblingReference_IsExcusedByAWaiver()
	{
		// Arrange
		// A legitimate qualified project that must reference a sibling has a recorded escape.
		ArchitecturePolicy policy = new()
		{
			Version = 2,
			Responsibilities = [Persistence],
			Waivers =
			[
				new PolicyWaiver
				{
					Project = "Persistence.SqlServer",
					Rules = [ArchitectureRules.SiblingIndependence],
					Reason = "A tool that must share the SQL Server adapter; reviewed and accepted for now.",
				},
			],
		};

		var evaluator = Evaluator(
			policy,
			[Project("Persistence.SqlServer", ["Persistence.PostgreSql"]), Project("Persistence.PostgreSql")],
			[]
		);

		// Act
		var violations = evaluator.SiblingIndependence();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task WaiverIntegrity_GivenAnUnknownRuleId_ReportsIt()
	{
		// Arrange
		// A typo must not silently excuse nothing.
		ArchitecturePolicy policy = new()
		{
			Version = 2,
			Responsibilities = [Core],
			Waivers =
			[
				new PolicyWaiver
				{
					Project = "Core",
					Rules = ["siblingIndependance"],
					Reason = "A reason long enough to pass the schema's minimum length requirement.",
				},
			],
		};

		var evaluator = Evaluator(policy, [Project("Core")], []);

		// Act
		var violations = evaluator.WaiverIntegrity();

		// Assert
		await Assert.That(violations).IsNotEmpty();
		await Assert
			.That(violations.Any(violation => violation.Message.Contains("unknown rule", StringComparison.Ordinal)))
			.IsTrue();
	}

	[Test]
	public async Task EvaluateAll_GivenAnAdditionalRule_ReportsItsViolation()
	{
		// Arrange
		var evaluator = Evaluator(Policy(Core), [Project("Core")], [], [new AlwaysViolatesRule("noConsoleLogging")]);

		// Act
		var violations = evaluator.EvaluateAll();

		// Assert
		await Assert.That(violations.Any(violation => violation.Rule == "noConsoleLogging")).IsTrue();
	}

	[Test]
	public async Task EvaluateAdditionalRule_GivenAWaiverExcusingTheRule_ReportsNothing()
	{
		// Arrange
		ArchitecturePolicy policy = new()
		{
			Version = 2,
			Responsibilities = [Core],
			Waivers =
			[
				new PolicyWaiver
				{
					Project = "Core",
					Rules = ["noConsoleLogging"],
					Reason = "The logging facade is not available in this project yet; revisit next quarter.",
				},
			],
		};

		var evaluator = Evaluator(policy, [Project("Core")], [], [new AlwaysViolatesRule("noConsoleLogging")]);

		// Act
		var violations = evaluator.EvaluateAdditionalRule("noConsoleLogging");

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	/// <summary>A bespoke rule a repository might add, used to prove the extension point.</summary>
	/// <param name="rule">The rule identifier.</param>
	sealed class AlwaysViolatesRule(string rule) : IArchitectureRule
	{
		/// <inheritdoc />
		public string Rule { get; } = rule;

		/// <inheritdoc />
		public IReadOnlyList<ArchitectureViolation> Evaluate(ArchitectureContext context) =>
			[new ArchitectureViolation(Rule, "Core", "A bespoke check failed.")];
	}
}
