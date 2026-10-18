namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Exercises the closure opt-in, prefix collapsing and the restore fallback.</summary>
public sealed class ForbiddenPackageScopeTests
{
	[Test]
	public async Task ForbiddenPackages_GivenClosureScope_ReportsInheritedPackages()
	{
		// Arrange
		// The opt-in closure scope restores the strict compile-graph behaviour.
		var target = ForbiddenPackageOwnershipTests.Target(
			direct: [],
			owned: [],
			resolved: ["Microsoft.EntityFrameworkCore"],
			through: new() { ["Persistence"] = ["Microsoft.EntityFrameworkCore"] }
		);

		var evaluator = ForbiddenPackageOwnershipTests.Evaluator(
			ForbiddenPackageOwnershipTests.Policy(scope: "closure"),
			target
		);

		// Act
		var violations = evaluator.ForbiddenPackages();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("restore closure");
	}

	[Test]
	public async Task ForbiddenPackages_GivenRelatedPackages_ReportsOneFindingPerPrefix()
	{
		// Arrange
		var target = ForbiddenPackageOwnershipTests.Target(
			direct: ["Microsoft.EntityFrameworkCore"],
			owned:
			[
				"Microsoft.EntityFrameworkCore",
				"Microsoft.EntityFrameworkCore.Abstractions",
				"Microsoft.EntityFrameworkCore.Analyzers",
			],
			resolved:
			[
				"Microsoft.EntityFrameworkCore",
				"Microsoft.EntityFrameworkCore.Abstractions",
				"Microsoft.EntityFrameworkCore.Analyzers",
			],
			through: new() { ["Persistence"] = [] }
		);

		var evaluator = ForbiddenPackageOwnershipTests.Evaluator(ForbiddenPackageOwnershipTests.Policy(), target);

		// Act
		var violations = evaluator.ForbiddenPackages();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("related package");
	}

	[Test]
	public async Task ForbiddenPackages_GivenNoPackageGraph_ChecksDeclaredPackages()
	{
		// Arrange
		// Before a restore there is no ownership data, so the declaration is all that can be checked.
		var evaluator = ForbiddenPackageOwnershipTests.Evaluator(
			ForbiddenPackageOwnershipTests.Policy(),
			target: null,
			packageReferences: ["Microsoft.EntityFrameworkCore.SqlServer"]
		);

		// Act
		var violations = evaluator.ForbiddenPackages();

		// Assert
		await Assert.That(violations).HasSingleItem();
	}

	[Test]
	public async Task ForbiddenPackages_GivenAnIncompleteRestore_FallsBackToOwnedPackages()
	{
		// Arrange
		// The permitted reference has no node in the assets file, so its contribution is unknown.
		// The rule must not guess: only the packages the project owns outright are checked.
		var target = ForbiddenPackageOwnershipTests.Target(
			direct: ["Api.Logging"],
			owned: ["Api.Logging"],
			resolved: ["Api.Logging", "Microsoft.EntityFrameworkCore"],
			through: new()
		);

		var evaluator = ForbiddenPackageOwnershipTests.Evaluator(ForbiddenPackageOwnershipTests.Policy(), target);

		// Act
		var violations = evaluator.ForbiddenPackages();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task OwnedPackages_GivenAPermittedReference_ExcludesItsPackages()
	{
		// Arrange
		var target = ForbiddenPackageOwnershipTests.Target(
			direct: ["Api.Logging"],
			owned: ["Api.Logging"],
			resolved: ["Api.Logging", "Microsoft.EntityFrameworkCore"],
			through: new() { ["Persistence"] = ["Microsoft.EntityFrameworkCore"] }
		);

		var evaluator = ForbiddenPackageOwnershipTests.Evaluator(ForbiddenPackageOwnershipTests.Policy(), target);

		// Act
		var owned = evaluator.OwnedPackages(evaluator.Projects["Api"]);

		// Assert
		await Assert.That(owned).Contains("Api.Logging");
		await Assert.That(owned).DoesNotContain("Microsoft.EntityFrameworkCore");
	}
}
