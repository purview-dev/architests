namespace Purview.Architecture.Testing.UnitTests;

/// <summary>
/// Exercises containment of the paths a policy supplies, happy path and unhappy path.
/// </summary>
/// <remarks>
/// A policy arrives with a pull request, so every path it names is input. Each scenario that
/// rejects an escape is paired with one proving an ordinary path still resolves.
/// </remarks>
public sealed class PolicyPathTests
{
	static string Root => Path.Combine(Path.GetTempPath(), "PurviewPolicyPath", "repo");

	[Test]
	public async Task Resolve_GivenARepoRelativePath_ResolvesInsideTheRepository()
	{
		// Act
		var resolved = PolicyPath.Resolve(Root, "src/Core", "sourceRoots");

		// Assert
		await Assert.That(resolved).IsEqualTo(Path.GetFullPath(Path.Combine(Root, "src", "Core")));
	}

	[Test]
	public async Task Resolve_GivenTheRootItself_ResolvesToTheRoot()
	{
		// Arrange
		// An empty value is how a flat layout says "scan the repository root".

		// Act
		var resolved = PolicyPath.Resolve(Root, string.Empty, "sourceRoots");

		// Assert
		await Assert.That(resolved).IsEqualTo(Path.GetFullPath(Root));
	}

	[Test]
	public async Task Resolve_GivenANestedTraversalThatStaysInside_Resolves()
	{
		// Arrange
		// '..' is only a problem when it leaves the repository.

		// Act
		var resolved = PolicyPath.Resolve(Root, "src/Core/../Infra", "sourceRoots");

		// Assert
		await Assert.That(resolved).IsEqualTo(Path.GetFullPath(Path.Combine(Root, "src", "Infra")));
	}

	[Test]
	public async Task Resolve_GivenATraversalOutOfTheRepository_Throws()
	{
		// Act
		var act = () => PolicyPath.Resolve(Root, "../../elsewhere", "sourceRoots");

		// Assert
		var exception = await Assert.That(act).Throws<InvalidOperationException>();
		await Assert.That(exception!.Message).Contains("sourceRoots");
		await Assert.That(exception.Message).Contains("outside the repository");
	}

	/// <summary>
	/// The reason Path.Combine alone is not enough: it discards the root entirely when the second
	/// argument is already rooted, so an absolute path silently escapes without any '..'.
	/// </summary>
	[Test]
	public async Task Resolve_GivenAnAbsolutePath_Throws()
	{
		// Arrange
		var absolute = OperatingSystem.IsWindows() ? @"C:\Windows\System32" : "/etc";

		// Act
		var act = () => PolicyPath.Resolve(Root, absolute, "assemblyRules.assemblyRoots");

		// Assert
		var exception = await Assert.That(act).Throws<InvalidOperationException>();
		await Assert.That(exception!.Message).Contains("assemblyRules.assemblyRoots");
	}

	/// <summary>A sibling whose name merely starts with the root's name is still outside it.</summary>
	[Test]
	public async Task Resolve_GivenASiblingSharingANamePrefix_Throws()
	{
		// Act
		var act = () => PolicyPath.Resolve(Root, "../repo-other", "sourceRoots");

		// Assert
		await Assert.That(act).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task TryResolve_GivenAnEscape_ReportsFalseRatherThanThrowing()
	{
		// Act
		var contained = PolicyPath.TryResolve(Root, "../../elsewhere", out var resolved);

		// Assert
		await Assert.That(contained).IsFalse();
		await Assert.That(resolved).IsEmpty();
	}

	[Test]
	public async Task TryResolve_GivenARepoRelativePath_ReportsTrue()
	{
		// Act
		var contained = PolicyPath.TryResolve(Root, "src", out var resolved);

		// Assert
		await Assert.That(contained).IsTrue();
		await Assert.That(resolved).IsEqualTo(Path.GetFullPath(Path.Combine(Root, "src")));
	}
}
