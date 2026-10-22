namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Exercises package ownership: what a project owns versus what a reference explains.</summary>
public sealed class ForbiddenPackageOwnershipTests
{
	internal static ArchitecturePolicy Policy(string scope = "own", string[]? mustNot = null) =>
		new()
		{
			Version = 3,
			Responsibilities =
			[
				new Responsibility
				{
					Name = "Api",
					Category = "host",
					Purpose = "The host that composes the application.",
					MayDependOn = ["Persistence"],
					MustNotDependOn = mustNot ?? [],
					ForbiddenPackagePrefixes = ["Microsoft.EntityFrameworkCore"],
					ForbiddenPackageScope = scope,
				},
				new Responsibility
				{
					Name = "Persistence",
					Category = "infrastructure",
					Purpose = "The adapter that owns the database technology.",
					MayDependOn = [],
					MustNotDependOn = [],
				},
			],
		};

	internal static PackageTarget Target(
		string[] direct,
		string[] owned,
		string[] resolved,
		Dictionary<string, string[]> through
	) =>
		new(
			"net10.0",
			direct,
			owned,
			resolved,
			through.ToDictionary(
				pair => pair.Key,
				pair => (IReadOnlyList<string>)pair.Value,
				StringComparer.OrdinalIgnoreCase
			)
		);

	internal static ArchitecturePolicyEvaluator Evaluator(
		ArchitecturePolicy policy,
		PackageTarget? target,
		IReadOnlyList<string>? packageReferences = null
	)
	{
		ProjectNode api = new("Api", "/repo/src/Api/Api.csproj", ["Persistence"], packageReferences ?? [], false);

		if (target is not null)
			api = api with { PackageTargets = [target], ResolvedPackages = target.Resolved };

		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
		{
			["Api"] = api,
			["Persistence"] = new("Persistence", "/repo/src/Persistence/Persistence.csproj", [], [], false),
		};

		return new ArchitecturePolicyEvaluator(policy, projects, new Dictionary<string, AssemblyNode>());
	}

	[Test]
	public async Task ForbiddenPackages_GivenAPermittedProjectReference_DoesNotReportItsPackages()
	{
		// Arrange
		// The host may reference Persistence; EF Core is the adapter's choice, not the host's.
		var target = Target(
			direct: [],
			owned: [],
			resolved: ["Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Abstractions"],
			through: new()
			{
				["Persistence"] = ["Microsoft.EntityFrameworkCore", "Microsoft.EntityFrameworkCore.Abstractions"],
			}
		);

		var evaluator = Evaluator(Policy(), target);

		// Act
		var violations = evaluator.ForbiddenPackages();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task ForbiddenPackages_GivenADirectPackage_ReportsIt()
	{
		// Arrange
		// The host declares EF Core itself as well as referencing the adapter.
		var target = Target(
			direct: ["Microsoft.EntityFrameworkCore"],
			owned: ["Microsoft.EntityFrameworkCore"],
			resolved: ["Microsoft.EntityFrameworkCore"],
			through: new() { ["Persistence"] = ["Microsoft.EntityFrameworkCore"] }
		);

		var evaluator = Evaluator(Policy(), target);

		// Act
		var violations = evaluator.ForbiddenPackages();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("own package references");
	}

	[Test]
	public async Task ForbiddenPackages_GivenAPackageInjectedByBuildProperties_ReportsIt()
	{
		// Arrange
		// The csproj declares nothing, but Directory.Build.props injected the package: restore lists
		// it as a direct dependency, so it is owned.
		var target = Target(
			direct: ["Microsoft.EntityFrameworkCore"],
			owned: ["Microsoft.EntityFrameworkCore"],
			resolved: ["Microsoft.EntityFrameworkCore"],
			through: new() { ["Persistence"] = [] }
		);

		var evaluator = Evaluator(Policy(), target);

		// Act
		var violations = evaluator.ForbiddenPackages();

		// Assert
		await Assert.That(violations).HasSingleItem();
	}

	[Test]
	public async Task ForbiddenPackages_GivenATransitiveOfAnOwnPackage_ReportsIt()
	{
		// Arrange
		// The host declares Api.Data; EF Core arrives through that package, not a project reference.
		var target = Target(
			direct: ["Api.Data"],
			owned: ["Api.Data", "Microsoft.EntityFrameworkCore"],
			resolved: ["Api.Data", "Microsoft.EntityFrameworkCore"],
			through: new() { ["Persistence"] = [] }
		);

		var evaluator = Evaluator(Policy(), target);

		// Act
		var violations = evaluator.ForbiddenPackages();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("own package references");
	}

	[Test]
	public async Task ForbiddenPackages_GivenADisallowedProjectReference_ReportsItsPackages()
	{
		// Arrange
		// The reference is forbidden, so its packages are not explained by a permitted reference.
		var target = Target(
			direct: [],
			owned: [],
			resolved: ["Microsoft.EntityFrameworkCore"],
			through: new() { ["Persistence"] = ["Microsoft.EntityFrameworkCore"] }
		);

		var evaluator = Evaluator(Policy(mustNot: ["Persistence"]), target);

		// Act
		var violations = evaluator.ForbiddenPackages();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("No permitted project reference");
	}
}
