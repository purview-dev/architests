namespace Purview.Architecture.Testing.UnitTests;

/// <summary>
/// Exercises <c>waiverPolicy.maxAgeDays</c> and the waiver integrity checks around it.
/// </summary>
/// <remarks>
/// The review date is what turns "reviewed" from a note into an obligation. The clock is pinned so
/// the tests state an age in days rather than depending on when the suite runs.
/// </remarks>
public sealed class WaiverExpiryTests
{
	static readonly DateOnly Today = new(2026, 10, 18);

	static Responsibility Core =>
		new()
		{
			Name = "Core",
			Category = "domain",
			Purpose = "The fundamental domain model and its rules.",
			MayDependOn = [],
			MustNotDependOn = [],
		};

	static ArchitecturePolicy Policy(int? maxAgeDays, params PolicyWaiver[] waivers) =>
		new()
		{
			Version = 3,
			Responsibilities = [Core],
			WaiverPolicy = new WaiverPolicySettings { MaxAgeDays = maxAgeDays },
			Waivers = waivers,
		};

	static PolicyWaiver Waiver(string? reviewedOn, string rule = ArchitectureRules.Naming) =>
		new()
		{
			Project = "Core",
			Rules = [rule],
			Reason = "A reviewed departure that is long enough to satisfy the schema's minimum length.",
			ReviewedOn = reviewedOn ?? string.Empty,
		};

	static ArchitecturePolicyEvaluator Evaluator(ArchitecturePolicy policy) =>
		new(
			policy,
			new Dictionary<string, ProjectNode> { ["Core"] = new("Core", "/repo/src/Core/Core.csproj", [], [], false) },
			new Dictionary<string, AssemblyNode>(),
			today: Today
		);

	[Test]
	public async Task WaiverIntegrity_GivenAWaiverOlderThanTheMaximum_ReportsIt()
	{
		// Arrange
		// 31 days old against a 30-day maximum.
		var evaluator = Evaluator(Policy(30, Waiver("2026-09-17")));

		// Act
		var violations = evaluator.WaiverIntegrity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("31 days ago");
		await Assert.That(violations[0].Message).Contains("beyond waiverPolicy.maxAgeDays of 30");
	}

	[Test]
	public async Task WaiverIntegrity_GivenAWaiverWithinTheMaximum_ReportsNothing()
	{
		// Arrange
		var evaluator = Evaluator(Policy(30, Waiver("2026-09-18")));

		// Act
		var violations = evaluator.WaiverIntegrity();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task WaiverIntegrity_GivenNoReviewDateWithExpiryOn_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(Policy(30, Waiver(reviewedOn: null)));

		// Act
		var violations = evaluator.WaiverIntegrity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("has no reviewedOn date");
	}

	[Test]
	public async Task WaiverIntegrity_GivenNoReviewDateWithExpiryOff_ReportsNothing()
	{
		// Arrange
		// Expiry is opt-in: a policy that has not adopted it still accepts an undated waiver.
		var evaluator = Evaluator(Policy(maxAgeDays: null, Waiver(reviewedOn: null)));

		// Act
		var violations = evaluator.WaiverIntegrity();

		// Assert
		await Assert.That(violations).IsEmpty();
	}

	[Test]
	public async Task WaiverIntegrity_GivenAReviewDateInTheFuture_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(Policy(30, Waiver("2026-10-19")));

		// Act
		var violations = evaluator.WaiverIntegrity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("in the future");
	}

	[Test]
	public async Task WaiverIntegrity_GivenAMalformedReviewDate_ReportsIt()
	{
		// Arrange
		var evaluator = Evaluator(Policy(maxAgeDays: null, Waiver("18/10/2026")));

		// Act
		var violations = evaluator.WaiverIntegrity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("not a date in the form yyyy-MM-dd");
	}

	[Test]
	public async Task WaiverIntegrity_GivenAWaiverForAnUnwaivableRule_ReportsIt()
	{
		// Arrange
		// A waiver that names a policy guard would look meaningful and excuse nothing.
		var evaluator = Evaluator(Policy(30, Waiver("2026-10-18", ArchitectureRules.PolicyIntegrity)));

		// Act
		var violations = evaluator.WaiverIntegrity();

		// Assert
		await Assert.That(violations).HasSingleItem();
		await Assert.That(violations[0].Message).Contains("cannot be waived");
	}

	[Test]
	public async Task WaiverReviewer_GivenEveryWaiver_ReportsOneReviewInPolicyOrder()
	{
		// Arrange
		var policy = Policy(
			30,
			Waiver("2026-10-18"),
			new PolicyWaiver
			{
				Project = "Core",
				Rules = [ArchitectureRules.Naming],
				Reason = "A second reviewed departure that is long enough to satisfy the schema's minimum length.",
				ReviewedOn = "2026-08-01",
			}
		);

		// Act
		var reviews = WaiverReviewer.Review(policy, Today);

		// Assert
		await Assert.That(reviews.Count).IsEqualTo(2);
		await Assert.That(reviews[0].IsStale).IsFalse();
		await Assert.That(reviews[1].IsStale).IsTrue();
	}
}
