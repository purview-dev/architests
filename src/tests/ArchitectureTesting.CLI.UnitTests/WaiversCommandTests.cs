using System.Globalization;
using Purview.Architecture.CLI;

namespace Purview.Architecture.Testing.CLI.UnitTests;

/// <summary>Exercises <c>architests waivers list</c>.</summary>
public sealed class WaiversCommandTests
{
	[Test]
	public async Task Run_GivenWaiversListAndNoWaivers_Succeeds()
	{
		// Arrange
		var root = ToolFixture.CreateRepository(ToolFixture.ValidPolicy);

		try
		{
			// Act
			var (exitCode, output, _) = ToolFixture.Run("waivers", "list", "--repository", root);

			// Assert
			await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
			await Assert.That(output).Contains("No waivers.");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Run_GivenAStaleWaiverWithoutTheStaleFlag_ListsItAndSucceeds()
	{
		// Arrange
		// The listing is informational; only --stale turns a stale waiver into a failing run.
		var root = ToolFixture.CreateRepository(ToolFixture.StaleWaiver);

		try
		{
			// Act
			var (exitCode, output, _) = ToolFixture.Run("waivers", "list", "--repository", root);

			// Assert
			await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
			await Assert.That(output).Contains("Legacy.Utilities [naming]");
			await Assert.That(output).Contains("beyond waiverPolicy.maxAgeDays of 30");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Run_GivenAStaleWaiverAndTheStaleFlag_ReturnsFindings()
	{
		// Arrange
		var root = ToolFixture.CreateRepository(ToolFixture.StaleWaiver);

		try
		{
			// Act
			var (exitCode, output, _) = ToolFixture.Run("waivers", "list", "--stale", "--repository", root);

			// Assert
			await Assert.That(exitCode).IsEqualTo(CommandLine.Findings);
			await Assert.That(output).Contains("Legacy.Utilities");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Run_GivenWaiversWithoutTheListVerb_ReturnsUsageError()
	{
		// Act
		var (exitCode, _, error) = ToolFixture.Run("waivers");

		// Assert
		await Assert.That(exitCode).IsEqualTo(CommandLine.UsageError);
		await Assert.That(error).Contains("Expected 'list'");
	}

	[Test]
	public async Task Run_GivenWaiversListWithOnlyACurrentWaiver_SaysThereAreNoStaleOnes()
	{
		// Arrange
		// The same waiver, reviewed today, is inside the 30-day maximum.
		var reviewedOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		var policy = ToolFixture.StaleWaiver.Replace("2020-01-01", reviewedOn, StringComparison.Ordinal);
		var root = ToolFixture.CreateRepository(policy);

		try
		{
			// Act
			var (exitCode, output, _) = ToolFixture.Run("waivers", "list", "--stale", "--repository", root);

			// Assert
			await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
			await Assert.That(output).Contains("No stale waivers.");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}
}
