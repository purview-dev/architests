using System.Globalization;

namespace Purview.Architecture.Testing;

/// <summary>The review state of one waiver.</summary>
/// <param name="Waiver">The waiver being reviewed.</param>
/// <param name="IsStale">Whether the waiver is older than the policy allows, or is undated or malformed.</param>
/// <param name="Reason">Why the waiver needs attention, or why it is current.</param>
public sealed record WaiverReview(PolicyWaiver Waiver, bool IsStale, string Reason);

/// <summary>Applies <c>waiverPolicy.maxAgeDays</c> to a policy's waivers.</summary>
/// <remarks>
/// A waiver nobody revisits is how a policy quietly stops being true. The review is shared by the
/// <c>waiverIntegrity</c> rule and by <c>architests waivers list</c>, so the test and the command
/// cannot disagree about which waivers are due.
/// </remarks>
public static class WaiverReviewer
{
	/// <summary>The date format a waiver's <c>reviewedOn</c> uses.</summary>
	public const string DateFormat = "yyyy-MM-dd";

	/// <summary>Reviews every waiver against the policy's maximum age.</summary>
	/// <param name="policy">The policy whose waivers are reviewed.</param>
	/// <param name="today">The date the review is performed, so a test can pin it.</param>
	/// <returns>One review per waiver, in policy order.</returns>
	public static IReadOnlyList<WaiverReview> Review(ArchitecturePolicy policy, DateOnly today)
	{
		ArgumentNullException.ThrowIfNull(policy);

		var maxAgeDays = policy.WaiverPolicy.MaxAgeDays;

		return [.. policy.Waivers.Select(waiver => ReviewWaiver(waiver, maxAgeDays, today))];
	}

	/// <summary>Reviews one waiver.</summary>
	/// <param name="waiver">The waiver.</param>
	/// <param name="maxAgeDays">The maximum age, or <see langword="null"/> when expiry is off.</param>
	/// <param name="today">The date the review is performed.</param>
	/// <returns>The review.</returns>
	static WaiverReview ReviewWaiver(PolicyWaiver waiver, int? maxAgeDays, DateOnly today)
	{
		if (string.IsNullOrWhiteSpace(waiver.ReviewedOn))
		{
			return maxAgeDays is null
				? new WaiverReview(waiver, false, "no review date; waiverPolicy.maxAgeDays is off")
				: new WaiverReview(
					waiver,
					true,
					"has no reviewedOn date, so its age cannot be checked against waiverPolicy.maxAgeDays. "
						+ "Add the date it was last reviewed."
				);
		}

		if (
			!DateOnly.TryParseExact(
				waiver.ReviewedOn,
				DateFormat,
				CultureInfo.InvariantCulture,
				DateTimeStyles.None,
				out var reviewedOn
			)
		)
		{
			return new WaiverReview(
				waiver,
				true,
				$"has reviewedOn '{waiver.ReviewedOn}', which is not a date in the form yyyy-MM-dd, so it cannot expire."
			);
		}

		if (reviewedOn > today)
		{
			return new WaiverReview(
				waiver,
				true,
				$"was reviewed on {waiver.ReviewedOn}, which is in the future. A review cannot predate the departure it records."
			);
		}

		if (maxAgeDays is null)
			return new WaiverReview(waiver, false, $"reviewed {waiver.ReviewedOn}; waiverPolicy.maxAgeDays is off");

		var age = today.DayNumber - reviewedOn.DayNumber;

		return age > maxAgeDays
			? new WaiverReview(
				waiver,
				true,
				$"was last reviewed on {waiver.ReviewedOn}, {age} days ago, beyond waiverPolicy.maxAgeDays of "
					+ $"{maxAgeDays}. Review it and update reviewedOn, or remove it."
			)
			: new WaiverReview(waiver, false, $"reviewed {waiver.ReviewedOn}, {age} days ago");
	}
}
