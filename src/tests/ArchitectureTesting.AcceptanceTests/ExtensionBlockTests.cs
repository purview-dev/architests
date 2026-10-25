using BDTest;

namespace Purview.Architecture.Testing.AcceptanceTests;

/// <summary>
/// How the type-level rules treat a C# extension block, happy path and unhappy path.
/// </summary>
/// <remarks>
/// The compiler lowers an extension block into a nested container named <c>&lt;G&gt;$&lt;hash&gt;</c>
/// that carries no attributes. Every scenario here pairs a <b>happy path</b> - the container is
/// never reported - with an <b>unhappy path</b> proving the same rule still catches the real
/// violation sitting beside it. Without the pair, "reports nothing" could just mean the rule
/// stopped working.
/// </remarks>
[Story(
	AsA = "developer adopting the architecture tests",
	IWant = "the type rules to ignore types the compiler invented",
	SoThat = "I am only ever asked to fix code I actually wrote"
)]
public class ExtensionBlockTests : TUnitBDTestBase<PolicyContext>
{
	RepositorySteps Repository => new(Context);
	RuleSteps Rules => new(Context);
	OutcomeSteps Outcome => new(Context);

	[Test]
	[ScenarioText("Type shape ignores the container an extension block produces")]
	public async Task TypeShape_HappyPath() =>
		await Given(() => Repository.AGovernedProject())
			.And(() => Repository.ItDeclaresAnExtensionBlock("OrderExtensions"))
			.When(() => Rules.TheTypeShapeRuleRuns())
			.Then(() => Outcome.NothingIsReported());

	[Test]
	[ScenarioText("Type shape still catches a real utility class beside an extension block")]
	public async Task TypeShape_UnhappyPath() =>
		await Given(() => Repository.AGovernedProject())
			.And(() => Repository.ItDeclaresAnExtensionBlock("OrderExtensions"))
			.And(() => Repository.ItDeclaresAnUnsealedClass("OrderService"))
			.When(() => Rules.TheTypeShapeRuleRuns())
			.Then(() => Outcome.ExactlyOneViolationIsReported())
			.And(() => Outcome.TheViolationNames("OrderService"))
			.And(() => Outcome.NoViolationMentionsASynthesizedType());

	[Test]
	[ScenarioText("Type namespace ignores the container an extension block produces")]
	public async Task TypeNamespace_HappyPath() =>
		await Given(() => Repository.AGovernedProject())
			.And(() => Repository.ItDeclaresAnExtensionBlock("OrderExtensions"))
			.When(() => Rules.TheTypeNamespaceRuleRuns())
			.Then(() => Outcome.NothingIsReported());

	[Test]
	[ScenarioText("Type namespace reports the author's class, not the container beneath it")]
	public async Task TypeNamespace_UnhappyPath() =>
		await Given(() => Repository.AGovernedProject())
			// The container inherits the outer type's namespace, so both are outside the root.
			// Only the one an author can move is reported.
			.And(() => Repository.ItDeclaresAnExtensionBlockIn("OrderExtensions", "Contoso.Legacy"))
			.When(() => Rules.TheTypeNamespaceRuleRuns())
			.Then(() => Outcome.ExactlyOneViolationIsReported())
			.And(() => Outcome.TheViolationNames("OrderExtensions"))
			.And(() => Outcome.NoViolationMentionsASynthesizedType());

	[Test]
	[ScenarioText("Type naming ignores the container an extension block produces")]
	public async Task TypeNaming_HappyPath() =>
		await Given(() => Repository.AGovernedProject())
			.And(() => Repository.ItDeclaresAnExtensionBlock("OrderExtensions"))
			.When(() => Rules.TheTypeNamingRuleRuns())
			.Then(() => Outcome.NothingIsReported());

	[Test]
	[ScenarioText("Type naming still catches an interface missing its I prefix")]
	public async Task TypeNaming_UnhappyPath() =>
		await Given(() => Repository.AGovernedProject())
			.And(() => Repository.ItDeclaresAnExtensionBlock("OrderExtensions"))
			.And(() => Repository.ItDeclaresAnInterface("OrderStore"))
			.When(() => Rules.TheTypeNamingRuleRuns())
			.Then(() => Outcome.ExactlyOneViolationIsReported())
			.And(() => Outcome.TheViolationNames("OrderStore"))
			.And(() => Outcome.NoViolationMentionsASynthesizedType());

	[Test]
	[ScenarioText("The captured API omits the container an extension block produces")]
	public async Task APICapture_HappyPath() =>
		await Given(() => Repository.AGovernedProject())
			.And(() => Repository.ItDeclaresAnExtensionBlock("OrderExtensions"))
			.When(() => Rules.ThePublicAPIIsCaptured())
			.Then(() => Outcome.TheCapturedAPIRecordsNoSynthesizedType());

	[Test]
	[ScenarioText("The captured API still records the static class the author wrote")]
	public async Task APICapture_UnhappyPath() =>
		await Given(() => Repository.AGovernedProject())
			.And(() => Repository.ItDeclaresAnExtensionBlock("OrderExtensions"))
			.And(() => Repository.ItDeclaresASealedClass("Order"))
			.When(() => Rules.ThePublicAPIIsCaptured())
			.Then(() => Outcome.TheCapturedAPIRecords("OrderExtensions"))
			.And(() => Outcome.TheCapturedAPIRecords("Order"))
			.And(() => Outcome.TheCapturedAPIRecordsNoSynthesizedType());

	/// <summary>
	/// The escape hatch, documented as behaviour: <c>excludeGeneratedCode</c> is the single switch
	/// that decides whether generated code is governed, and turning it off governs the synthesized
	/// container too. It is off-by-default for exactly the reason this scenario shows.
	/// </summary>
	[Test]
	[ScenarioText("Turning generated-code exclusion off is what surfaces the container")]
	public async Task GeneratedCodeNotExcluded_SurfacesTheContainer() =>
		await Given(() => Repository.AGovernedProjectIncludingGeneratedCode())
			.And(() => Repository.ItDeclaresAnExtensionBlock("OrderExtensions"))
			.When(() => Rules.TheTypeShapeRuleRuns())
			.Then(() => Outcome.AtLeastOneViolationIsReported())
			.And(() => Outcome.TheViolationNames("<G>$"));
}
