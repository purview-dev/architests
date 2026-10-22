using System.Text.Json.Serialization;

namespace Purview.Architecture.Testing;

/// <summary>The type-level rules: naming, shape, namespace and attributes.</summary>
/// <remarks>
/// Off by default because it reads compiled assemblies and needs a build to have produced them. Once
/// enabled, every public type in a governed responsibility is checked; internal types are outside
/// the surface a consumer can call and are not policed.
/// </remarks>
public sealed class TypeRules
{
	/// <summary>Whether the type-level rules run.</summary>
	[JsonPropertyName("enabled")]
	public bool Enabled { get; init; }

	/// <summary>The responsibilities the rules govern. Empty applies them to every production responsibility.</summary>
	[JsonPropertyName("appliesTo")]
	public TypeRuleScope AppliesTo { get; init; } = new();

	/// <summary>Whether compiler- and tool-generated types are skipped. Defaults to true.</summary>
	[JsonPropertyName("excludeGeneratedCode")]
	public bool ExcludeGeneratedCode { get; init; } = true;

	/// <summary>Namespace prefixes whose types are skipped, such as a generated-client namespace.</summary>
	[JsonPropertyName("excludeNamespacePrefixes")]
	public IReadOnlyList<string> ExcludeNamespacePrefixes { get; init; } = [];

	/// <summary>
	/// Type-name suffixes that describe a mechanism rather than a responsibility. Defaults to
	/// <c>Manager</c>, <c>Helper</c>, <c>Util</c> and <c>Impl</c>.
	/// </summary>
	[JsonPropertyName("discouragedTypeSuffixes")]
	public IReadOnlyList<string> DiscouragedTypeSuffixes { get; init; } = ["Manager", "Helper", "Util", "Impl"];

	/// <summary>Whether a public concrete class must be sealed. Defaults to true.</summary>
	[JsonPropertyName("sealConcreteClasses")]
	public bool SealConcreteClasses { get; init; } = true;

	/// <summary>
	/// Whether a public property or method must not expose <c>List&lt;T&gt;</c> or
	/// <c>Dictionary&lt;TKey, TValue&gt;</c>. Defaults to true.
	/// </summary>
	[JsonPropertyName("requireReadOnlyCollections")]
	public bool RequireReadOnlyCollections { get; init; } = true;

	/// <summary>Attributes every governed public type must carry, by namespace-qualified type name.</summary>
	[JsonPropertyName("forbiddenAttributes")]
	public IReadOnlyList<string> ForbiddenAttributes { get; init; } = [];

	/// <summary>Attributes no governed public type may carry, by namespace-qualified type name.</summary>
	[JsonPropertyName("requiredAttributes")]
	public IReadOnlyList<string> RequiredAttributes { get; init; } = [];

	/// <summary>Whether <c>[Obsolete]</c> may be applied without a message. Defaults to false.</summary>
	[JsonPropertyName("allowObsoleteWithoutMessage")]
	public bool AllowObsoleteWithoutMessage { get; init; }

	/// <summary>
	/// Whether <c>InternalsVisibleTo</c> must not name a production assembly. Defaults to true:
	/// exposing internals to production code is a boundary leak, while exposing them to a test
	/// project is ordinary.
	/// </summary>
	[JsonPropertyName("forbidInternalsVisibleToProduction")]
	public bool ForbidInternalsVisibleToProduction { get; init; } = true;
}

/// <summary>Which responsibilities the type-level rules govern.</summary>
public sealed class TypeRuleScope
{
	/// <summary>
	/// The categories to govern, such as <c>domain</c> or <c>shared</c>. Empty governs every
	/// production responsibility.
	/// </summary>
	[JsonPropertyName("categories")]
	public IReadOnlyList<string> Categories { get; init; } = [];
}
