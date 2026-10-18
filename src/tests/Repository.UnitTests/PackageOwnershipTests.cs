using Purview.Architecture.Testing;

namespace Purview.Architecture.Repository;

/// <summary>
/// Verifies package ownership against this repository's own restore graph: the CLI consumes the
/// package's dependencies through a permitted project reference, so it does not own them.
/// </summary>
public sealed class PackageOwnershipTests
{
	[Test]
	public async Task OwnedPackages_GivenAPermittedProjectReference_ExcludesItsPackages()
	{
		// Arrange
		var root = ArchitecturePolicy.RepositoryRoot();
		var projects = ProjectGraph.Load(root, ["src/src"]);
		ArchitecturePolicyEvaluator evaluator = new(ArchitecturePolicy.Load(root), projects);
		var cli = projects["Architecture.CLI"];

		// Act
		var owned = evaluator.OwnedPackages(cli);

		// Assert
		await Assert
			.That(cli.PackageTargets)
			.IsNotEmpty()
			.Because("the ownership check reads restore output, so the solution must be built first");

		await Assert
			.That(cli.ResolvedPackages)
			.Contains("JsonSchema.Net")
			.Because("the package arrives through the ArchitectureTesting project reference");

		await Assert
			.That(owned)
			.DoesNotContain("JsonSchema.Net")
			.Because("JsonSchema.Net is explained by the permitted ArchitectureTesting reference");

		await Assert
			.That(cli.PackageTargets[0].ThroughReferences.ContainsKey("ArchitectureTesting"))
			.IsTrue()
			.Because("restore keys the project by its assembly name, so the graph maps it back");
	}
}
