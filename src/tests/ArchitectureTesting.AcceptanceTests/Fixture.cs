namespace Purview.Architecture.Testing.AcceptanceTests;

/// <summary>Builds the in-memory type surfaces the behaviour scenarios declare.</summary>
/// <remarks>
/// Deliberately local to this project. The unit tests have their own builder; sharing one would
/// couple two suites that are meant to fail independently.
/// </remarks>
static class Fixture
{
	/// <summary>Builds a public type in the scenario's project.</summary>
	public static APIType Type(
		string name,
		APITypeKind kind = APITypeKind.Class,
		bool isPublic = true,
		bool isAbstract = false,
		bool isSealed = true,
		string ns = PolicyContext.RootNamespace,
		string? fullName = null,
		bool isNested = false,
		IReadOnlyList<APIMember>? members = null,
		IReadOnlyList<APIAttribute>? attributes = null
	) =>
		new(
			PolicyContext.Project,
			ns,
			name,
			fullName ?? (ns.Length == 0 ? name : $"{ns}.{name}"),
			kind,
			isPublic,
			isAbstract,
			isSealed,
			isNested,
			null,
			[],
			members ?? [],
			attributes ?? []
		);

	/// <summary>Builds a public member.</summary>
	public static APIMember Member(
		string name,
		APIMemberKind kind = APIMemberKind.Method,
		string typeName = "System.Void",
		bool isStatic = false
	) => new(name, kind, typeName, isStatic, []);
}
