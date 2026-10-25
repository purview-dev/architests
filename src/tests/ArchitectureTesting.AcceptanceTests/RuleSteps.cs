using BDTest;

namespace Purview.Architecture.Testing.AcceptanceTests;

/// <summary>The "when" steps: which rule runs over the repository.</summary>
public sealed class RuleSteps(PolicyContext context)
{
	[StepText("the type shape rule runs")]
	public void TheTypeShapeRuleRuns() => context.Violations = context.Evaluator().TypeShape();

	[StepText("the type namespace rule runs")]
	public void TheTypeNamespaceRuleRuns() => context.Violations = context.Evaluator().TypeNamespace();

	[StepText("the type naming rule runs")]
	public void TheTypeNamingRuleRuns() => context.Violations = context.Evaluator().TypeNaming();

	[StepText("the public API is captured")]
	public void ThePublicAPIIsCaptured()
	{
		Dictionary<string, APIAssembly> surfaces = new(StringComparer.OrdinalIgnoreCase)
		{
			[PolicyContext.Project] = new(PolicyContext.Project, PolicyContext.Project, [], [.. context.Types]),
		};

		context.Baseline = APIBaseline.Capture(surfaces, context.Policy());
	}
}
