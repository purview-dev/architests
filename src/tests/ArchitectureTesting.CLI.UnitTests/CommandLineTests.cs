using Purview.Architecture.CLI;

namespace Purview.Architecture.Testing.CLI.UnitTests;

/// <summary>Exercises the parser and the <c>validate</c> command.</summary>
public sealed class CommandLineTests
{
	[Test]
	public async Task Run_GivenNoArguments_WritesUsageAndSucceeds()
	{
		// Act
		var (exitCode, output, _) = ToolFixture.Run();

		// Assert
		await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
		await Assert.That(output).Contains("Usage:");
		await Assert.That(output).Contains("validate");
	}

	[Test]
	public async Task Run_GivenHelp_WritesUsageAndSucceeds()
	{
		// Act
		var (exitCode, output, _) = ToolFixture.Run("--help");

		// Assert
		await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
		await Assert.That(output).Contains("architests <command> [options]");
	}

	[Test]
	public async Task Run_GivenVersion_WritesTheVersionAndSucceeds()
	{
		// Act
		var (exitCode, output, _) = ToolFixture.Run("--version");

		// Assert
		await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
		await Assert.That(output.Trim()).IsEqualTo(CommandLine.Version);
	}

	[Test]
	public async Task Run_GivenAnUnknownCommand_ReturnsUsageError()
	{
		// Act
		var (exitCode, _, error) = ToolFixture.Run("frobnicate");

		// Assert
		await Assert.That(exitCode).IsEqualTo(CommandLine.UsageError);
		await Assert.That(error).Contains("Unknown command 'frobnicate'");
	}

	[Test]
	public async Task Run_GivenValidateAndAValidPolicy_Succeeds()
	{
		// Arrange
		var root = ToolFixture.CreateRepository(ToolFixture.ValidPolicy);

		try
		{
			// Act
			var (exitCode, output, error) = ToolFixture.Run("validate", "--repository", root);

			// Assert
			await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
			await Assert.That(output).Contains("Policy is valid");
			await Assert.That(error).IsEmpty();
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Run_GivenValidateAndASchemaBreach_ReturnsFindings()
	{
		// Arrange
		var root = ToolFixture.CreateRepository(ToolFixture.SchemaBreach);

		try
		{
			// Act
			var (exitCode, _, error) = ToolFixture.Run("validate", "--repository", root);

			// Assert
			await Assert.That(exitCode).IsEqualTo(CommandLine.Findings);
			await Assert.That(error).Contains("does not match its schema");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Run_GivenValidateAndAnIntegrityBreach_ReturnsFindingsThatNameTheCorrection()
	{
		// Arrange
		var root = ToolFixture.CreateRepository(ToolFixture.IntegrityBreach);

		try
		{
			// Act
			var (exitCode, _, error) = ToolFixture.Run("validate", $"--repository={root}");

			// Assert
			await Assert.That(exitCode).IsEqualTo(CommandLine.Findings);
			await Assert.That(error).Contains("is not internally consistent");
			await Assert.That(error).Contains("mayDependOn names 'Missing'");
		}
		finally
		{
			Directory.Delete(root, recursive: true);
		}
	}

	[Test]
	public async Task Run_GivenValidateAndAMissingRepository_ReturnsFindings()
	{
		// Arrange
		var missing = Path.Combine(Path.GetTempPath(), "PurviewArchitestsCli", Guid.NewGuid().ToString("N"));

		// Act
		var (exitCode, _, error) = ToolFixture.Run("validate", "--repository", missing);

		// Assert
		await Assert.That(exitCode).IsEqualTo(CommandLine.Findings);
		await Assert.That(error).Contains("does not exist");
	}

	[Test]
	public async Task Run_GivenValidateAndAnUnknownOption_ReturnsUsageError()
	{
		// Act
		var (exitCode, _, error) = ToolFixture.Run("validate", "--frobnicate");

		// Assert
		await Assert.That(exitCode).IsEqualTo(CommandLine.UsageError);
		await Assert.That(error).Contains("Unknown option '--frobnicate'");
	}

	[Test]
	public async Task Run_GivenValidateAndHelp_WritesTheCommandUsage()
	{
		// Act
		var (exitCode, output, _) = ToolFixture.Run("validate", "--help");

		// Assert
		await Assert.That(exitCode).IsEqualTo(CommandLine.Success);
		await Assert.That(output).Contains("architests validate");
	}
}
