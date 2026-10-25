using BDTest;

namespace Purview.Architecture.Testing.AcceptanceTests;

/// <summary>The "then" steps: what the rule reported.</summary>
public sealed class OutcomeSteps(PolicyContext context)
{
	[StepText("nothing is reported")]
	public async Task NothingIsReported() =>
		await Assert.That(context.Violations).IsEmpty().Because(ArchitecturePolicyEvaluator.Report(context.Violations));

	[StepText("exactly one violation is reported")]
	public async Task ExactlyOneViolationIsReported() =>
		await Assert
			.That(context.Violations)
			.HasSingleItem()
			.Because(ArchitecturePolicyEvaluator.Report(context.Violations));

	[StepText("at least one violation is reported")]
	public async Task AtLeastOneViolationIsReported() => await Assert.That(context.Violations).IsNotEmpty();

	[StepText("the violation names {name}")]
	public async Task TheViolationNames(string name) =>
		await Assert
			.That(context.Violations.Any(violation => violation.Message.Contains(name, StringComparison.Ordinal)))
			.IsTrue()
			.Because(ArchitecturePolicyEvaluator.Report(context.Violations));

	[StepText("no violation mentions a compiler-synthesized type")]
	public async Task NoViolationMentionsASynthesizedType() =>
		await Assert
			.That(context.Violations.Any(violation => violation.Message.Contains("<G>$", StringComparison.Ordinal)))
			.IsFalse()
			.Because(
				"a synthesized container is not in anyone's source, so asking an author to change it is unactionable"
			);

	[StepText("the captured API records {name}")]
	public async Task TheCapturedAPIRecords(string name) =>
		await Assert.That(CapturedTypeNames()).Contains(type => type.Contains(name, StringComparison.Ordinal));

	[StepText("the captured API records no compiler-synthesized type")]
	public async Task TheCapturedAPIRecordsNoSynthesizedType() =>
		await Assert
			.That(CapturedTypeNames().Any(type => type.Contains("<G>$", StringComparison.Ordinal)))
			.IsFalse()
			.Because("a hash in a captured name would churn the committed contract on every nearby edit");

	/// <summary>The type names the capture recorded.</summary>
	/// <returns>The names, empty when no capture ran.</returns>
	IReadOnlyList<string> CapturedTypeNames() =>
		[.. (context.Baseline?.Projects ?? []).SelectMany(project => project.Types).Select(type => type.Name)];
}
