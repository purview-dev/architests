namespace Purview.Architecture.Testing.UnitTests;

/// <summary>Builds in-memory type surfaces for the type-rule tests.</summary>
static class TypeRuleFixture
{
	/// <summary>The default enabled settings, before a test overrides a field.</summary>
	public static TypeRules Rules => new() { Enabled = true };

	/// <summary>Builds a public type in the <c>Core</c> project.</summary>
	public static APIType Type(
		string name,
		APITypeKind kind = APITypeKind.Class,
		bool isPublic = true,
		bool isAbstract = false,
		bool isSealed = true,
		string ns = "Purview.Core",
		string? baseType = null,
		IReadOnlyList<APIMember>? members = null,
		IReadOnlyList<APIAttribute>? attributes = null,
		string project = "Core",
		IReadOnlyList<string>? interfaces = null,
		string? fullName = null,
		bool isNested = false
	) =>
		new(
			project,
			ns,
			name,
			fullName ?? (ns.Length == 0 ? name : $"{ns}.{name}"),
			kind,
			isPublic,
			isAbstract,
			isSealed,
			isNested,
			baseType,
			interfaces ?? [],
			members ?? [],
			attributes ?? []
		);

	/// <summary>Builds a public member.</summary>
	public static APIMember Member(
		string name,
		APIMemberKind kind = APIMemberKind.Method,
		string typeName = "System.Void",
		bool isStatic = false,
		IReadOnlyList<APIAttribute>? attributes = null,
		IReadOnlyList<string>? parameters = null,
		int genericArity = 0
	) =>
		new(name, kind, typeName, isStatic, attributes ?? [])
		{
			Parameters = parameters ?? [],
			GenericArity = genericArity,
		};

	/// <summary>Builds an evaluator over one governed project.</summary>
	public static ArchitecturePolicyEvaluator Evaluator(TypeRules rules, params APIType[] types)
	{
		ArchitecturePolicy policy = new()
		{
			Version = 3,
			Responsibilities =
			[
				new Responsibility
				{
					Name = "Core",
					Category = "shared",
					Purpose = "A responsibility declared by the type-rule fixture.",
					MayDependOn = [],
					MustNotDependOn = [],
				},
			],
			TypeRules = rules,
		};

		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
		{
			["Core"] = new("Core", "/repo/src/Core/Core.csproj", [], [], false) { RootNamespace = "Purview.Core" },
		};

		Dictionary<string, APIAssembly> surfaces = new(StringComparer.OrdinalIgnoreCase)
		{
			["Core"] = new("Core", "Core", [], types),
		};

		return new ArchitecturePolicyEvaluator(
			policy,
			projects,
			new Dictionary<string, AssemblyNode>(),
			apiSurfaces: surfaces
		);
	}
}
