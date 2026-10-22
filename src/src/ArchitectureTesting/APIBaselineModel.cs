namespace Purview.Architecture.Testing;

/// <summary>A project's public API in a baseline.</summary>
/// <param name="Project">The project name.</param>
/// <param name="Responsibility">The responsibility the project maps to, for review context.</param>
/// <param name="Types">The public types the project declares.</param>
public sealed record APIBaselineProject(string Project, string Responsibility, IReadOnlyList<APIBaselineType> Types);

/// <summary>A public type in a baseline.</summary>
/// <param name="Name">The namespace-qualified type name.</param>
/// <param name="Kind">The type kind, as its enum name.</param>
/// <param name="IsAbstract">Whether the type is abstract.</param>
/// <param name="IsSealed">Whether the type is sealed.</param>
/// <param name="BaseType">The base type, when there is one.</param>
/// <param name="Interfaces">The implemented interfaces.</param>
/// <param name="Members">The public members.</param>
public sealed record APIBaselineType(
	string Name,
	string Kind,
	bool IsAbstract,
	bool IsSealed,
	string? BaseType,
	IReadOnlyList<string> Interfaces,
	IReadOnlyList<APIBaselineMember> Members
);

/// <summary>A public member in a baseline.</summary>
/// <param name="Kind">The member kind, as its enum name.</param>
/// <param name="Name">The member name.</param>
/// <param name="TypeName">The declared type.</param>
/// <param name="IsStatic">Whether the member is static.</param>
/// <param name="Parameters">The parameter types, in order.</param>
public sealed record APIBaselineMember(
	string Kind,
	string Name,
	string TypeName,
	bool IsStatic,
	IReadOnlyList<string> Parameters
);
