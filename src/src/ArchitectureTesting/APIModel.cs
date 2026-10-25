using System.Diagnostics.CodeAnalysis;

namespace Purview.Architecture.Testing;

/// <summary>The kind of a type declared in an assembly.</summary>
public enum APITypeKind
{
	/// <summary>A class, including a record class.</summary>
	Class,

	/// <summary>A value type, including a record struct.</summary>
	Struct,

	/// <summary>An interface.</summary>
	Interface,

	/// <summary>An enumeration.</summary>
	Enum,

	/// <summary>A delegate.</summary>
	Delegate,
}

/// <summary>The kind of a member declared on a type.</summary>
public enum APIMemberKind
{
	/// <summary>A method that is not a property, event or operator accessor.</summary>
	Method,

	/// <summary>A property, including an indexer.</summary>
	Property,

	/// <summary>A field, including a constant.</summary>
	Field,

	/// <summary>An event.</summary>
	Event,

	/// <summary>An instance or static constructor.</summary>
	Constructor,
}

/// <summary>An attribute applied to a type, member, or assembly.</summary>
/// <param name="TypeName">The attribute's namespace-qualified type name.</param>
/// <param name="StringArgument">
/// The attribute's first string argument, when it has one. Only the shape rules need it, for
/// <c>[Obsolete("...")]</c> and <c>[InternalsVisibleTo("...")]</c>.
/// </param>
[SuppressMessage(
	"Naming",
	"CA1711:Identifiers should not have incorrect suffix",
	Justification = "This record models an attribute instance; it is not itself an attribute class."
)]
public sealed record APIAttribute(string TypeName, string? StringArgument);

/// <summary>A public member declared on a type.</summary>
/// <param name="Name">The member's metadata name.</param>
/// <param name="Kind">Whether it is a method, property, field, event or constructor.</param>
/// <param name="TypeName">
/// The declared type: the return type, property type, field type or event delegate type. A
/// constructor uses <c>System.Void</c>.
/// </param>
/// <param name="IsStatic">Whether the member is static.</param>
/// <param name="Attributes">The attributes applied to the member.</param>
public sealed record APIMember(
	string Name,
	APIMemberKind Kind,
	string TypeName,
	bool IsStatic,
	IReadOnlyList<APIAttribute> Attributes
)
{
	/// <summary>The parameter types, in order. Empty for a field, property or event.</summary>
	public IReadOnlyList<string> Parameters { get; init; } = [];

	/// <summary>The number of generic parameters the member declares. Zero for a non-generic member.</summary>
	/// <remarks>
	/// Arity is part of a member's identity: <c>M&lt;T&gt;(X)</c> and <c>M(X)</c> are distinct overloads
	/// that share a name and a parameter list, so a key that omits arity collides on them.
	/// </remarks>
	public int GenericArity { get; init; }
}

/// <summary>A compiled assembly's declaration surface and assembly-level attributes.</summary>
/// <param name="ProjectName">The project the assembly belongs to.</param>
/// <param name="AssemblyName">The assembly's simple name.</param>
/// <param name="Attributes">The assembly-level attributes.</param>
/// <param name="Types">The types the assembly declares, including internal and nested types.</param>
public sealed record APIAssembly(
	string ProjectName,
	string AssemblyName,
	IReadOnlyList<APIAttribute> Attributes,
	IReadOnlyList<APIType> Types
)
{
	/// <summary>The assembly file path, used to find its XML documentation beside it.</summary>
	public string Path { get; init; } = string.Empty;
}

/// <summary>A type declared in a compiled assembly.</summary>
/// <param name="ProjectName">The project the assembly belongs to, or empty when it is not a project.</param>
/// <param name="Namespace">The type's namespace, or empty for a global type.</param>
/// <param name="Name">The simple metadata name, without the generic arity suffix.</param>
/// <param name="FullName">The namespace-qualified name, including any declaring type.</param>
/// <param name="Kind">Whether it is a class, struct, interface, enum or delegate.</param>
/// <param name="IsPublic">Whether the type is public, including a public nested type.</param>
/// <param name="IsAbstract">Whether the type is abstract. A static class is also sealed.</param>
/// <param name="IsSealed">Whether the type is sealed.</param>
/// <param name="IsNested">Whether the type is declared inside another type.</param>
/// <param name="BaseType">The namespace-qualified base type, when there is one.</param>
/// <param name="Interfaces">The interfaces the type implements.</param>
/// <param name="Members">The public members the type declares.</param>
/// <param name="Attributes">The attributes applied to the type.</param>
public sealed record APIType(
	string ProjectName,
	string Namespace,
	string Name,
	string FullName,
	APITypeKind Kind,
	bool IsPublic,
	bool IsAbstract,
	bool IsSealed,
	bool IsNested,
	string? BaseType,
	IReadOnlyList<string> Interfaces,
	IReadOnlyList<APIMember> Members,
	IReadOnlyList<APIAttribute> Attributes
)
{
	/// <summary>When true the type is a static class: abstract and sealed in metadata.</summary>
	public bool IsStatic => Kind == APITypeKind.Class && IsAbstract && IsSealed;

	/// <summary>When true the compiler or a tool marked the type as generated.</summary>
	/// <remarks>
	/// Attribute-marked generation only. A compiler-synthesized type is often unmarked, so
	/// <see cref="IsCompilerSynthesized"/> answers the other half.
	/// </remarks>
	public bool IsGeneratedCode =>
		Attributes.Any(attribute =>
			attribute.TypeName
				is "System.CodeDom.Compiler.GeneratedCodeAttribute"
					or "System.Runtime.CompilerServices.CompilerGeneratedAttribute"
		);

	/// <summary>When true the compiler synthesized the type and no source declares it.</summary>
	/// <remarks>
	/// A metadata name beginning with <c>&lt;</c> is reserved for the compiler, because the character
	/// is not legal in a C# identifier. It covers the <c>&lt;G&gt;$…</c> and <c>&lt;M&gt;$…</c>
	/// containers an extension block emits, lambda display classes, iterator and async state
	/// machines, and <c>&lt;PrivateImplementationDetails&gt;</c>. None of these carry
	/// <c>[CompilerGenerated]</c> reliably - the extension containers carry no attributes at all - so
	/// the name is the only signal, and a rule that reports one asks an author to change something
	/// that does not exist in their source.
	/// </remarks>
	public bool IsCompilerSynthesized => IsReservedName(Name) || FullName.Split('.').Any(IsReservedName);

	/// <summary>Reports whether a metadata name uses the compiler's reserved form.</summary>
	/// <param name="name">The metadata name.</param>
	/// <returns><see langword="true"/> when the name begins with <c>&lt;</c>.</returns>
	static bool IsReservedName(string name) => name.Length > 0 && name[0] == '<';

	/// <summary>When true the type is an attribute class.</summary>
	public bool IsAttribute => string.Equals(BaseType, "System.Attribute", StringComparison.Ordinal);

	/// <summary>When true the type is an exception class, directly or through a known exception base.</summary>
	public bool IsException =>
		string.Equals(BaseType, "System.Exception", StringComparison.Ordinal)
		|| (
			BaseType is not null
			&& BaseType.EndsWith("Exception", StringComparison.Ordinal)
			&& !BaseType.Contains('<', StringComparison.Ordinal)
		);
}
