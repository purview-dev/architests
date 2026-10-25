using System.Text.Json;
using BDTest;

namespace Purview.Architecture.Testing.AcceptanceTests;

/// <summary>Given/when/then steps for how a policy document itself is treated.</summary>
public sealed class PolicyDocumentSteps(PolicyDocumentContext context)
{
	// -- Given ---------------------------------------------------------------------------------

	[StepText("a policy whose sourceRoots is {root}")]
	public void APolicyWithSourceRoot(string root) => context.WritePolicy(Policy($"\"sourceRoots\": [{Json(root)}],"));

	[StepText("a policy whose assemblyRoots is {root}")]
	public void APolicyWithAssemblyRoot(string root) =>
		context.WritePolicy(Policy($"\"assemblyRules\": {{ \"enabled\": true, \"assemblyRoots\": [{Json(root)}] }},"));

	[StepText("a policy whose assemblyGlobs is {glob}")]
	public void APolicyWithAssemblyGlob(string glob) =>
		context.WritePolicy(
			Policy(
				"\"sourceRoots\": [\"src\"],"
					+ $"\"assemblyRules\": {{ \"enabled\": true, \"assemblyRoots\": [\"src\"], \"assemblyGlobs\": [{Json(glob)}] }},"
			)
		);

	[StepText("a policy whose apiRules.baseline is {path}")]
	public void APolicyWithBaseline(string path) =>
		context.WritePolicy(Policy($"\"apiRules\": {{ \"enabled\": true, \"baseline\": {Json(path)} }},"));

	[StepText("a policy containing the key {key}")]
	public void APolicyContainingTheKey(string key) =>
		// A string value, so the scenario turns on whether the key is recognised rather than on
		// whether its value happens to bind to the declared type.
		context.WritePolicy(Policy($"{Json(key)}: \"a value\","));

	[StepText("a policy with two waivers for the project {project}")]
	public void APolicyWithTwoWaivers(string project) =>
		context.WritePolicy(
			Policy(
				"\"waivers\": ["
					+ $"{{ \"project\": {Json(project)}, \"rules\": [\"typeNaming\"], \"reason\": \"The first waiver, with a reason long enough to satisfy the schema's minimum length.\" }},"
					+ $"{{ \"project\": {Json(project)}, \"rules\": [\"typeShape\"], \"reason\": \"The second waiver, with a reason long enough to satisfy the schema's minimum length.\" }}"
					+ "],"
			)
		);

	[StepText("the repository declares the project {project}")]
	public void TheRepositoryDeclaresTheProject(string project)
	{
		var folder = Path.Combine(context.Root, "src", project);
		Directory.CreateDirectory(folder);
		File.WriteAllText(Path.Combine(folder, $"{project}.csproj"), """<Project Sdk="Microsoft.NET.Sdk"></Project>""");
	}

	// -- When ----------------------------------------------------------------------------------

	[StepText("the policy is read")]
	public void ThePolicyIsRead() => Attempt(() => context.Policy = ArchitecturePolicy.Load(context.Root));

	[StepText("the project graph is loaded")]
	public void TheProjectGraphIsLoaded() =>
		Attempt(() =>
		{
			context.Policy = ArchitecturePolicy.Load(context.Root);
			context.Projects = ProjectGraph.Load(context.Root, context.Policy.SourceRoots);
		});

	[StepText("the assemblies are discovered")]
	public void TheAssembliesAreDiscovered() =>
		Attempt(() =>
		{
			context.Policy = ArchitecturePolicy.Load(context.Root);
			AssemblyGraph.Discover(context.Root, context.Policy.AssemblyRules);
		});

	[StepText("the evaluator is built for the repository")]
	public void TheEvaluatorIsBuiltForTheRepository() =>
		Attempt(() => ArchitecturePolicyEvaluator.ForRepository(context.Root));

	// -- Then ----------------------------------------------------------------------------------

	[StepText("it is accepted")]
	public async Task ItIsAccepted() => await Assert.That(context.Failure?.Message ?? "none").IsEqualTo("none");

	[StepText("it is rejected, naming {fragment}")]
	public async Task ItIsRejectedNaming(string fragment)
	{
		await Assert.That(context.Failure).IsNotNull().Because("the policy should not have been accepted");
		await Assert.That(context.Failure!.Message).Contains(fragment, StringComparison.OrdinalIgnoreCase);
	}

	[StepText("the waiver integrity rule reports a duplicate")]
	public async Task TheWaiverIntegrityRuleReportsADuplicate()
	{
		var evaluator = ArchitecturePolicyEvaluator.ForRepository(context.Root);
		var violations = evaluator.WaiverIntegrity();

		await Assert
			.That(violations.Any(violation => violation.Message.Contains("waivers", StringComparison.Ordinal)))
			.IsTrue()
			.Because(ArchitecturePolicyEvaluator.Report(violations));
	}

	[StepText("every rule is run against the repository")]
	public void EveryRuleIsRunAgainstTheRepository() =>
		Attempt(() =>
		{
			var evaluator = ArchitecturePolicyEvaluator.ForRepository(context.Root);
			context.Violations = evaluator.EvaluateAll();
		});

	[StepText("no violation is reported")]
	public async Task NoViolationIsReported() =>
		await Assert
			.That(context.Violations ?? [])
			.IsEmpty()
			.Because(ArchitecturePolicyEvaluator.Report(context.Violations ?? []));

	[StepText("a {rule} violation is reported naming {project}")]
	public async Task AViolationIsReportedNaming(string rule, string project)
	{
		var violations = context.Violations ?? [];

		await Assert
			.That(
				violations.Any(violation =>
					string.Equals(violation.Rule, rule, StringComparison.Ordinal)
					&& violation.Message.Contains(project, StringComparison.Ordinal)
				)
			)
			.IsTrue()
			.Because(ArchitecturePolicyEvaluator.Report(violations));
	}

	[StepText("the waiver integrity rule reports nothing")]
	public async Task TheWaiverIntegrityRuleReportsNothing()
	{
		var evaluator = ArchitecturePolicyEvaluator.ForRepository(context.Root);
		var violations = evaluator.WaiverIntegrity();

		await Assert.That(violations).IsEmpty().Because(ArchitecturePolicyEvaluator.Report(violations));
	}

	/// <summary>Runs a step, recording the failure instead of throwing it.</summary>
	/// <param name="action">The step.</param>
	/// <remarks>
	/// Only the types the reader is documented to raise for a bad policy are caught. Anything
	/// else is a defect in the reader rather than a rejected policy, and must still fail the
	/// scenario rather than be recorded as an expected outcome.
	/// </remarks>
	void Attempt(Action action)
	{
		try
		{
			action();
		}
		catch (InvalidOperationException exception)
		{
			context.Failure = exception;
		}
		catch (JsonException exception)
		{
			context.Failure = exception;
		}
		catch (FileNotFoundException exception)
		{
			context.Failure = exception;
		}
	}

	/// <summary>Builds a minimal valid policy around an extra section.</summary>
	/// <param name="extra">The section under test, including its trailing comma.</param>
	/// <returns>The policy document.</returns>
	static string Policy(string extra) =>
		$$"""
			{
				"version": 3,
				{{extra}}
				"responsibilities": [
					{
						"name": "Core",
						"category": "shared",
						"purpose": "The responsibility the policy-document scenarios declare.",
						"mayDependOn": [],
						"mustNotDependOn": []
					}
				]
			}
			""";

	/// <summary>Renders a value as a JSON string, so a Windows path's backslashes survive.</summary>
	/// <param name="value">The value.</param>
	/// <returns>The quoted, escaped value.</returns>
	static string Json(string value) => JsonSerializer.Serialize(value);
}
