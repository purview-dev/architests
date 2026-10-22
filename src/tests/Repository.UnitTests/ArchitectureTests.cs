using Purview.Architecture.Testing;

namespace Purview.Architecture.Repository;

/// <summary>
/// Runs this repository's own architecture policy - through the package it ships - against this
/// repository.
/// </summary>
/// <remarks>
/// The policy lives at <c>.config/architecture-policy.json</c> and resolves by the default marker
/// walk, so the dogfood path is exactly the one a consumer takes. It enables
/// <c>assemblyRules</c>, so the type-level rule is exercised against the compiled package assembly
/// on every run.
/// </remarks>
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests
{
	[Test]
	public async Task APISurfaces_GivenEveryProductionProject_IsPresentAndPopulated()
	{
		// Arrange
		// A production project missing here escapes every type-level rule silently, because the
		// reader has no assembly to attribute to it.
		var missing = Evaluator
			.Projects.Values.Where(project => !project.IsTestProject)
			.Where(project => !Evaluator.APISurfaces.ContainsKey(project.Name))
			.Select(project => project.Name)
			.ToArray();

		// Act
		var publicTypes = Evaluator
			.APISurfaces.Values.SelectMany(assembly => assembly.Types)
			.Count(type => type.IsPublic);

		// Assert
		await Assert
			.That(missing)
			.IsEmpty()
			.Because(
				"the dogfood test project must reference every production project, or its assembly is invisible: "
					+ string.Join(", ", missing)
			);

		await Assert
			.That(publicTypes)
			.IsGreaterThan(0)
			.Because("an empty surface would make every type-level rule pass vacuously");
	}
}
