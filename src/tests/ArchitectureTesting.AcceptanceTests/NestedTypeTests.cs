using BDTest;

namespace Purview.Architecture.Testing.AcceptanceTests;

/// <summary>How the namespace rule treats a public nested type, happy path and unhappy path.</summary>
/// <remarks>
/// Metadata records no namespace on a nested type - it belongs to the type that declares it. Read
/// directly, every public nested type looks like it lives in the global namespace and so outside
/// its project's root. The pair here proves the resolution works without blunting the rule.
/// </remarks>
[Story(
	AsA = "developer adopting the architecture tests",
	IWant = "a nested type judged by the namespace of the type that declares it",
	SoThat = "nesting a type is not reported as moving it out of my root namespace"
)]
public class NestedTypeTests : TUnitBDTestBase<PolicyContext>
{
	RepositorySteps Repository => new(Context);
	RuleSteps Rules => new(Context);
	OutcomeSteps Outcome => new(Context);

	[Test]
	[ScenarioText("A nested type inside a correctly placed outer type is not reported")]
	public async Task NestedInsideTheRootNamespace_HappyPath() =>
		await Given(() => Repository.AGovernedProject())
			.And(() => Repository.ItDeclaresANestedClass("Builder", "Order", PolicyContext.RootNamespace))
			.When(() => Rules.TheTypeNamespaceRuleRuns())
			.Then(() => Outcome.NothingIsReported());

	[Test]
	[ScenarioText("A nested type inside a misplaced outer type is reported with it")]
	public async Task NestedOutsideTheRootNamespace_UnhappyPath() =>
		await Given(() => Repository.AGovernedProject())
			.And(() => Repository.ItDeclaresANestedClass("Builder", "Order", "Contoso.Legacy"))
			.When(() => Rules.TheTypeNamespaceRuleRuns())
			.Then(() => Outcome.TheViolationNames("Order"))
			.And(() => Outcome.TheViolationNames("Builder"));
}
