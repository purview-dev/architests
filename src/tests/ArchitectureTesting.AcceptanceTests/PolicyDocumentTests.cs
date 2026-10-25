using BDTest;

namespace Purview.Architecture.Testing.AcceptanceTests;

/// <summary>
/// How the reader treats the policy document itself: the paths it names, the globs it supplies,
/// the keys it contains, and the waivers it declares.
/// </summary>
/// <remarks>
/// A policy arrives with a pull request, including one from a fork, so it is input rather than
/// configuration the repository already trusted. Every scenario that rejects something is paired
/// with one proving the ordinary case still works - a reader that rejects everything would pass
/// the first half on its own.
/// </remarks>
[Story(
	AsA = "maintainer reviewing a pull request",
	IWant = "the policy file treated as input rather than as trusted configuration",
	SoThat = "a policy cannot read or write outside the repository, or quietly disable a rule"
)]
public class PolicyDocumentTests : TUnitBDTestBase<PolicyDocumentContext>
{
	PolicyDocumentSteps Steps => new(Context);

	// -- Source roots --------------------------------------------------------------------------

	[Test]
	[ScenarioText("A repo-relative source root is scanned")]
	public async Task SourceRoot_HappyPath() =>
		await Given(() => Steps.APolicyWithSourceRoot("src"))
			.And(() => Steps.TheRepositoryDeclaresTheProject("Core"))
			.When(() => Steps.TheProjectGraphIsLoaded())
			.Then(() => Steps.ItIsAccepted());

	[Test]
	[ScenarioText("A source root that climbs out of the repository is rejected")]
	public async Task SourceRoot_UnhappyPath() =>
		await Given(() => Steps.APolicyWithSourceRoot("../../elsewhere"))
			.When(() => Steps.TheProjectGraphIsLoaded())
			.Then(() => Steps.ItIsRejectedNaming("sourceRoots"));

	// -- Assembly roots ------------------------------------------------------------------------

	[Test]
	[ScenarioText("A repo-relative assembly root is scanned")]
	public async Task AssemblyRoot_HappyPath() =>
		await Given(() => Steps.APolicyWithAssemblyRoot("src"))
			.When(() => Steps.TheAssembliesAreDiscovered())
			.Then(() => Steps.ItIsAccepted());

	/// <summary>
	/// An absolute path needs no '..' to escape: Path.Combine discards its first argument entirely
	/// when the second is already rooted.
	/// </summary>
	[Test]
	[ScenarioText("An absolute assembly root is rejected")]
	public async Task AssemblyRoot_UnhappyPath() =>
		await Given(() => Steps.APolicyWithAssemblyRoot(OperatingSystem.IsWindows() ? @"C:\Windows" : "/etc"))
			.When(() => Steps.TheAssembliesAreDiscovered())
			.Then(() => Steps.ItIsRejectedNaming("assemblyRoots"));

	// -- The API baseline, which is a write target -----------------------------------------------

	[Test]
	[ScenarioText("A repo-relative API baseline is accepted")]
	public async Task Baseline_HappyPath() =>
		await Given(() => Steps.APolicyWithBaseline(".config/api-surface.json"))
			.When(() => Steps.TheEvaluatorIsBuiltForTheRepository())
			.Then(() => Steps.ItIsAccepted());

	[Test]
	[ScenarioText("An API baseline outside the repository is rejected")]
	public async Task Baseline_UnhappyPath() =>
		await Given(() => Steps.APolicyWithBaseline("../../stolen.json"))
			.When(() => Steps.TheEvaluatorIsBuiltForTheRepository())
			.Then(() => Steps.ItIsRejectedNaming("apiRules.baseline"));

	// -- Globs ---------------------------------------------------------------------------------

	[Test]
	[ScenarioText("An ordinary assembly glob is accepted")]
	public async Task Glob_HappyPath() =>
		await Given(() => Steps.APolicyWithAssemblyGlob("*.dll"))
			.And(() => Steps.TheRepositoryDeclaresTheProject("Core"))
			.When(() => Steps.TheAssembliesAreDiscovered())
			.Then(() => Steps.ItIsAccepted());

	/// <summary>
	/// The root has to exist for the glob to be used at all - a missing root is skipped, which is
	/// how this scenario first passed without exercising anything.
	/// </summary>
	[Test]
	[ScenarioText("A glob that is really a path is reported against the policy")]
	public async Task Glob_UnhappyPath() =>
		await Given(() => Steps.APolicyWithAssemblyGlob("../**/*.dll"))
			.And(() => Steps.TheRepositoryDeclaresTheProject("Core"))
			.When(() => Steps.TheAssembliesAreDiscovered())
			.Then(() => Steps.ItIsRejectedNaming("assemblyGlobs"));

	/// <summary>
	/// The traversal that confining the root alone does not stop. .NET accepts a relative path in
	/// a search pattern, so this enumerates the parent directory unless the pattern is validated
	/// too.
	/// </summary>
	[Test]
	[ScenarioText("A glob that climbs out of its root is rejected rather than followed")]
	public async Task Glob_EscapingRoot_IsRejected() =>
		await Given(() => Steps.APolicyWithAssemblyGlob(@"..\*.dll"))
			.And(() => Steps.TheRepositoryDeclaresTheProject("Core"))
			.When(() => Steps.TheAssembliesAreDiscovered())
			.Then(() => Steps.ItIsRejectedNaming("assemblyGlobs"));

	// -- Unknown keys ----------------------------------------------------------------------------

	/// <summary>
	/// The expensive failure this prevents: a mistyped group name leaves the group at its default,
	/// which is off, so the author believes a rule is enforced while nothing checks it.
	/// </summary>
	[Test]
	[ScenarioText("A mistyped rule group is rejected rather than ignored")]
	public async Task UnknownKey_UnhappyPath() =>
		await Given(() => Steps.APolicyContainingTheKey("typeRulez"))
			.When(() => Steps.ThePolicyIsRead())
			.Then(() => Steps.ItIsRejectedNaming("typeRulez"));

	[Test]
	[ScenarioText("A documentation-only key the schema allows is still accepted")]
	public async Task UnknownKey_HappyPath() =>
		await Given(() => Steps.APolicyContainingTheKey("description"))
			.When(() => Steps.ThePolicyIsRead())
			.Then(() => Steps.ItIsAccepted());

	// -- The whole rule set, outside TUnit ---------------------------------------------------------

	/// <summary>
	/// What `architests check` runs. Until it existed the full rule set was reachable only through
	/// a test project, so a repository without one could not ask the question at all.
	/// </summary>
	[Test]
	[ScenarioText("A repository matching its policy reports nothing")]
	public async Task EveryRule_HappyPath() =>
		await Given(() => Steps.APolicyWithSourceRoot("src"))
			.And(() => Steps.TheRepositoryDeclaresTheProject("Core"))
			.When(() => Steps.EveryRuleIsRunAgainstTheRepository())
			.Then(() => Steps.NoViolationIsReported());

	[Test]
	[ScenarioText("A project matching no responsibility is reported by name")]
	public async Task EveryRule_UnhappyPath() =>
		await Given(() => Steps.APolicyWithSourceRoot("src"))
			.And(() => Steps.TheRepositoryDeclaresTheProject("Core"))
			.And(() => Steps.TheRepositoryDeclaresTheProject("Rogue"))
			.When(() => Steps.EveryRuleIsRunAgainstTheRepository())
			.Then(() => Steps.AViolationIsReportedNaming("naming", "Rogue"));

	// -- Waivers ---------------------------------------------------------------------------------

	[Test]
	[ScenarioText("Two waivers for one project are reported, because only the first would apply")]
	public async Task DuplicateWaiver_UnhappyPath() =>
		await Given(() => Steps.APolicyWithTwoWaivers("Core"))
			.And(() => Steps.TheRepositoryDeclaresTheProject("Core"))
			.When(() => Steps.TheEvaluatorIsBuiltForTheRepository())
			.Then(() => Steps.TheWaiverIntegrityRuleReportsADuplicate());

	[Test]
	[ScenarioText("A single waiver for a project is not reported")]
	public async Task DuplicateWaiver_HappyPath() =>
		await Given(() => Steps.APolicyWithSourceRoot("src"))
			.And(() => Steps.TheRepositoryDeclaresTheProject("Core"))
			.When(() => Steps.TheEvaluatorIsBuiltForTheRepository())
			.Then(() => Steps.TheWaiverIntegrityRuleReportsNothing());
}
