using System.Text.Json.Serialization;

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
)
{
	/// <summary>The number of generic parameters the member declares. Zero for a non-generic member.</summary>
	/// <remarks>
	/// Written only when it is non-zero, so a baseline captured before arity was recorded stays byte
	/// for byte identical.
	/// </remarks>
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public int GenericArity { get; init; }

	/// <summary>
	/// The overload identity: everything that distinguishes a member from its siblings except the
	/// declared type.
	/// </summary>
	/// <remarks>
	/// A member cannot be identified by name and parameters alone. <c>M&lt;T&gt;(X)</c> and
	/// <c>M(X)</c> differ only in arity, and C# lets two members differ only in return type, so a key
	/// built from name and parameters collides on real, legal API. Overload identity deliberately
	/// stops short of the declared type: two members that match here and differ only in their type
	/// are reported as one changed member rather than as a removal and an addition.
	/// </remarks>
	[JsonIgnore]
	public string OverloadKey => $"{Kind}|{Name}|{GenericArity}|{string.Join(",", Parameters)}";

	/// <summary>The full identity, including the declared type.</summary>
	/// <remarks>
	/// Two members sharing this are indistinguishable. Real metadata cannot produce that, so a
	/// duplicate means the baseline file has been corrupted or hand-edited.
	/// </remarks>
	[JsonIgnore]
	public string Signature => $"{OverloadKey}|{TypeName}";
}
