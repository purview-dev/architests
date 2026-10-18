using Purview.Architecture.Testing;

namespace Purview.Architecture.Repository;

/// <summary>Fails when the rule catalogue and the documentation drift apart.</summary>
/// <remarks>
/// "Keep the docs in step" is a review instruction until it is a test. Every document that describes
/// the rule set must name every catalogue identifier in backticks, so adding a rule without
/// documenting it - or removing one without clearing the reference - fails the suite.
/// </remarks>
public sealed class RuleDocumentationTests
{
	static readonly string[] Documents =
	[
		"README.md",
		"src/src/ArchitectureTesting/README.md",
		"docs/Policy-Reference.md",
		"skills/purview-architests/SKILL.md",
	];

	[Test]
	public async Task RuleCatalogue_GivenEveryDocument_ListsEveryRule()
	{
		// Arrange
		var root = ArchitecturePolicy.RepositoryRoot();
		List<string> missing = [];

		// Act
		foreach (var document in Documents)
		{
			var path = Path.Combine(root, document);

			if (!File.Exists(path))
			{
				missing.Add($"{document} (missing)");
				continue;
			}

			var text = await File.ReadAllTextAsync(path);

			foreach (var rule in ArchitectureRules.All)
			{
				if (!text.Contains($"`{rule}`", StringComparison.Ordinal))
					missing.Add($"{document}: {rule}");
			}
		}

		// Assert
		await Assert
			.That(missing)
			.IsEmpty()
			.Because("the rule catalogue and the documentation disagree: " + string.Join(", ", missing));
	}

	[Test]
	public async Task RuleCatalogue_GivenADescriptor_HasAnAnchorAndASummary()
	{
		// Arrange
		var incomplete = ArchitectureRules
			.Catalogue.Where(descriptor =>
				string.IsNullOrWhiteSpace(descriptor.DocumentationAnchor)
				|| string.IsNullOrWhiteSpace(descriptor.Summary)
			)
			.Select(descriptor => descriptor.Rule)
			.ToArray();

		// Act / Assert
		await Assert
			.That(incomplete)
			.IsEmpty()
			.Because($"a rule without a summary or anchor cannot be documented: {string.Join(", ", incomplete)}");
	}
}
