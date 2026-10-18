namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Reads the package-scope switch from a policy document.</summary>
public sealed class ArchitecturePolicyReaderTests
{
	[Test]
	public async Task Load_GivenAForbiddenPackageScope_ReadsIt()
	{
		// Arrange
		var root = CreateRepository(
			/*lang=json,strict*/
			"""
			{
				"version": 3,
				"responsibilities": [
					{
						"name": "Api",
						"category": "host",
						"purpose": "The host that composes the application.",
						"mayDependOn": [],
						"mustNotDependOn": [],
						"forbiddenPackageScope": "closure"
					}
				]
			}
			"""
		);

		try
		{
			// Act
			var policy = ArchitecturePolicy.Load(root);

			// Assert
			await Assert.That(policy.Responsibilities[0].ForbiddenPackageScope).IsEqualTo("closure");

			var failures = PolicySchemaValidator.Validate(Path.Combine(root, ArchitecturePolicy.RelativePath));
			await Assert.That(failures).IsEmpty();
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Load_GivenNoForbiddenPackageScope_DefaultsToOwn()
	{
		// Arrange
		var root = CreateRepository(
			/*lang=json,strict*/
			"""
			{
				"version": 3,
				"responsibilities": [
					{
						"name": "Api",
						"category": "host",
						"purpose": "The host that composes the application.",
						"mayDependOn": [],
						"mustNotDependOn": []
					}
				]
			}
			"""
		);

		try
		{
			// Act
			var policy = ArchitecturePolicy.Load(root);

			// Assert
			await Assert.That(policy.Responsibilities[0].ForbiddenPackageScope).IsEqualTo("own");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	/// <summary>Writes a policy under a temporary repository root.</summary>
	/// <param name="policy">The policy document.</param>
	/// <returns>The repository root.</returns>
	static string CreateRepository(string policy)
	{
		var root = Directory.CreateTempSubdirectory("PurviewArchitecturePolicy").FullName;
		var config = Path.Combine(root, ".config");
		Directory.CreateDirectory(config);
		File.WriteAllText(Path.Combine(config, "architecture-policy.json"), policy);
		return root;
	}
}
