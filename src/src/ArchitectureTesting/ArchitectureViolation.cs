namespace Purview.Architecture.Testing;

/// <summary>A single breach of the architecture policy.</summary>
/// <param name="Rule">The rule identifier, so a caller can filter or waive by rule.</param>
/// <param name="Project">The offending project.</param>
/// <param name="Message">
/// What broke and what to do about it. A failure that only states the fact - "expected empty" -
/// costs the reader the whole investigation, so every message names the offending reference, the
/// direction the dependency should run, and the likely fix.
/// </param>
public sealed record ArchitectureViolation(string Rule, string Project, string Message)
{
	/// <inheritdoc />
	public override string ToString() => Message;
}

/// <summary>The rule identifiers the evaluator reports and a waiver may excuse.</summary>
public static class ArchitectureRules
{
	/// <summary>The policy contradicts itself: unknown names, overlapping permissions or duplicates.</summary>
	public const string PolicyIntegrity = "policyIntegrity";

	/// <summary>A project name that maps to no responsibility or alias.</summary>
	public const string Naming = "naming";

	/// <summary>A project named after a category rather than a responsibility.</summary>
	public const string DiscouragedName = "discouragedName";

	/// <summary>A reference the dependency matrix forbids or does not permit.</summary>
	public const string DependencyMatrix = "dependencyMatrix";

	/// <summary>A type reference the dependency matrix forbids or does not permit.</summary>
	public const string AssemblyDependencyMatrix = "assemblyDependencyMatrix";

	/// <summary>A public type whose name does not follow the convention its kind implies.</summary>
	public const string TypeNaming = "typeNaming";

	/// <summary>A public type whose declared shape weakens the boundary it sits on.</summary>
	public const string TypeShape = "typeShape";

	/// <summary>A public type declared outside its project's root namespace.</summary>
	public const string TypeNamespace = "typeNamespace";

	/// <summary>A public type or member whose attributes are missing, forbidden or misleading.</summary>
	public const string AttributePolicy = "attributePolicy";

	/// <summary>The public API differs from its committed baseline.</summary>
	public const string APISurface = "apiSurface";

	/// <summary>A public type or member has no XML documentation.</summary>
	public const string APIDocumentation = "apiDocumentation";

	/// <summary>A port has no public implementation in the repository.</summary>
	public const string PortImplementation = "portImplementation";

	/// <summary>A port's signature exposes an infrastructure technology.</summary>
	public const string PortPurity = "portPurity";

	/// <summary>A project references more, or fewer, projects than its dependency budget records.</summary>
	public const string DependencyBudgetRule = "dependencyBudget";

	/// <summary>A qualified implementation referencing a qualified sibling.</summary>
	public const string SiblingIndependence = "siblingIndependence";

	/// <summary>A non-host project referencing Composition.</summary>
	public const string CompositionBoundary = "compositionBoundary";

	/// <summary>A package whose identifier matches a forbidden prefix for the responsibility.</summary>
	public const string ForbiddenPackage = "forbiddenPackage";

	/// <summary>A framework reference matching a forbidden prefix for the responsibility.</summary>
	public const string ForbiddenFramework = "forbiddenFramework";

	/// <summary>A cycle in the project dependency graph.</summary>
	public const string Cycle = "cycle";

	/// <summary>Production code referencing a test project.</summary>
	public const string TestReference = "testReference";

	/// <summary>A waiver that is stale, unjustified or malformed.</summary>
	public const string WaiverIntegrity = "waiverIntegrity";

	/// <summary>
	/// No projects were discovered, so every other rule would pass vacuously.
	/// </summary>
	/// <remarks>
	/// The most dangerous result this package can produce is a green run that checked nothing. An
	/// empty graph satisfies every rule by construction, so emptiness is itself a violation.
	/// </remarks>
	public const string GraphDiscovery = "graphDiscovery";

	/// <summary>The standard rule set, with the metadata the docs and waivers are checked against.</summary>
	public static IReadOnlyList<ArchitectureRuleDescriptor> Catalogue { get; } =
	[
		new(
			PolicyIntegrity,
			"Policy integrity",
			"The policy names a responsibility that does not exist, permits and forbids the same dependency, or declares a duplicate name or alias.",
			"policy",
			Waivable: false,
			DocumentationAnchor: "policy-integrity"
		),
		new(
			GraphDiscovery,
			"Graph discovery",
			"No production projects were discovered, so every other rule would pass vacuously.",
			"project graph",
			Waivable: false,
			DocumentationAnchor: "graph-discovery"
		),
		new(
			Naming,
			"Responsibility naming",
			"A production project name maps to no responsibility or alias.",
			"project graph",
			Waivable: true,
			DocumentationAnchor: "naming"
		),
		new(
			DiscouragedName,
			"Discouraged project names",
			"A project is named after a category rather than a responsibility, so nothing can be kept out of it.",
			"project graph",
			Waivable: true,
			DocumentationAnchor: "discouraged-names"
		),
		new(
			DependencyMatrix,
			"Project dependency matrix",
			"A declared ProjectReference is forbidden or not permitted by the dependency matrix.",
			"project graph",
			Waivable: true,
			DocumentationAnchor: "dependency-matrix-semantics"
		),
		new(
			AssemblyDependencyMatrix,
			"Type-level dependency matrix",
			"A type in one responsibility uses a type in another, including a dependency that arrives transitively or through a package.",
			"assemblies",
			Waivable: true,
			DocumentationAnchor: "the-type-level-rule"
		),
		new(
			TypeNaming,
			"Type naming",
			"A public interface lacks the I prefix, an attribute or exception lacks its suffix, or a type ends with a discouraged suffix.",
			"assemblies",
			Waivable: true,
			Group: "typeRules",
			DocumentationAnchor: "type-naming"
		),
		new(
			TypeShape,
			"Type shape",
			"A public concrete class is not sealed, a public instance field is exposed, a mutable collection is returned, or a utility class is not static.",
			"assemblies",
			Waivable: true,
			Group: "typeRules",
			DocumentationAnchor: "type-shape"
		),
		new(
			TypeNamespace,
			"Type namespace",
			"A public type is declared outside its project's root namespace.",
			"assemblies",
			Waivable: true,
			Group: "typeRules",
			DocumentationAnchor: "type-namespace"
		),
		new(
			AttributePolicy,
			"Attribute policy",
			"A required attribute is missing, a forbidden one is present, Obsolete has no message, or InternalsVisibleTo names production code.",
			"assemblies",
			Waivable: true,
			Group: "typeRules",
			DocumentationAnchor: "attribute-policy"
		),
		new(
			APISurface,
			"API surface",
			"The public API differs from the committed baseline, whether by an addition, a removal or a shape change.",
			"assemblies",
			Waivable: true,
			Group: "apiRules",
			DocumentationAnchor: "api-surface"
		),
		new(
			APIDocumentation,
			"API documentation",
			"A public type or member has no XML documentation entry, or the project ships no documentation file.",
			"assemblies",
			Waivable: true,
			Group: "apiRules",
			DocumentationAnchor: "api-documentation"
		),
		new(
			PortImplementation,
			"Port implementation",
			"An interface in a port category has no public implementation in the repository.",
			"assemblies",
			Waivable: true,
			Group: "portRules",
			DocumentationAnchor: "port-implementation"
		),
		new(
			PortPurity,
			"Port purity",
			"A port's signature exposes a forbidden infrastructure technology.",
			"assemblies",
			Waivable: true,
			Group: "portRules",
			DocumentationAnchor: "port-purity"
		),
		new(
			DependencyBudgetRule,
			"Dependency budget",
			"A project references a project outside its budget, or the budget still allows a reference that has gone.",
			"project graph",
			Waivable: false,
			Group: "dependencyBudget",
			DocumentationAnchor: "dependency-budget"
		),
		new(
			SiblingIndependence,
			"Sibling independence",
			"A qualified implementation references a qualified sibling of the same responsibility.",
			"project graph",
			Waivable: true,
			DocumentationAnchor: "sibling-independence"
		),
		new(
			CompositionBoundary,
			"Composition boundary",
			"A non-composition, non-host project references the composition root.",
			"project graph",
			Waivable: true,
			DocumentationAnchor: "composition-boundary"
		),
		new(
			ForbiddenPackage,
			"Forbidden packages",
			"A package matches a forbidden prefix for the responsibility.",
			"project graph",
			Waivable: true,
			DocumentationAnchor: "forbidden-packages"
		),
		new(
			ForbiddenFramework,
			"Forbidden framework references",
			"A FrameworkReference matches a forbidden prefix for the responsibility.",
			"project graph",
			Waivable: true,
			DocumentationAnchor: "forbidden-framework-references"
		),
		new(
			Cycle,
			"Dependency cycles",
			"The project graph contains a cycle, reported as the path that closes it.",
			"project graph",
			Waivable: true,
			DocumentationAnchor: "cycles"
		),
		new(
			TestReference,
			"Production-to-test references",
			"A production project references a test project.",
			"project graph",
			Waivable: true,
			DocumentationAnchor: "test-references"
		),
		new(
			WaiverIntegrity,
			"Waiver integrity",
			"A waiver is stale, unjustified, malformed, or excuses an unknown or unwaivable rule.",
			"policy",
			Waivable: false,
			DocumentationAnchor: "waivers"
		),
	];

	/// <summary>Every rule the evaluator reports, so a waiver can be checked against a known set.</summary>
	/// <remarks>Derived from <see cref="Catalogue"/>, so a rule cannot be added to one and not the other.</remarks>
	public static IReadOnlyList<string> All { get; } = [.. Catalogue.Select(descriptor => descriptor.Rule)];

	/// <summary>Finds a rule's descriptor.</summary>
	/// <param name="rule">The rule identifier.</param>
	/// <returns>The descriptor, or <see langword="null"/> for a rule the repository added itself.</returns>
	public static ArchitectureRuleDescriptor? Describe(string rule) =>
		Catalogue.FirstOrDefault(descriptor =>
			string.Equals(descriptor.Rule, rule, StringComparison.OrdinalIgnoreCase)
		);

	/// <summary>Reports whether a policy waiver may excuse a rule.</summary>
	/// <param name="rule">The rule identifier.</param>
	/// <returns>
	/// <see langword="false"/> for the rules that guard the policy itself; <see langword="true"/> for
	/// an unknown identifier, because an additional rule is waivable unless it says otherwise.
	/// </returns>
	public static bool IsWaivable(string rule) => Describe(rule)?.Waivable ?? true;
}
