using System.Text.Json.Serialization;

namespace Purview.Architecture.Testing;

/// <summary>The committed-API rules: surface compatibility and documentation.</summary>
/// <remarks>
/// Off by default because both need a build. The baseline is deliberately a committed file: a change
/// to the public API is then a reviewed diff rather than an accident.
/// </remarks>
public sealed class APIRules
{
	/// <summary>Whether the API rules run.</summary>
	[JsonPropertyName("enabled")]
	public bool Enabled { get; init; }

	/// <summary>
	/// The baseline path relative to the repository root. Defaults to
	/// <c>.config/api-surface.json</c>.
	/// </summary>
	[JsonPropertyName("baseline")]
	public string Baseline { get; init; } = ".config/api-surface.json";

	/// <summary>Whether every public type must have an XML documentation entry. Defaults to true.</summary>
	[JsonPropertyName("requireTypeDocumentation")]
	public bool RequireTypeDocumentation { get; init; } = true;

	/// <summary>
	/// Whether every public member must have an XML documentation entry. Off by default: a record's
	/// positional parameters document its properties through the type's own entry.
	/// </summary>
	[JsonPropertyName("requireMemberDocumentation")]
	public bool RequireMemberDocumentation { get; init; }
}

/// <summary>The port rules: an interface's implementation and its technology purity.</summary>
public sealed class PortRules
{
	/// <summary>Whether the port rules run.</summary>
	[JsonPropertyName("enabled")]
	public bool Enabled { get; init; }

	/// <summary>
	/// The categories whose interfaces are treated as ports. Defaults to <c>domain</c> and
	/// <c>application</c>: an interface in an inner layer is a contract the outer layers implement.
	/// </summary>
	[JsonPropertyName("categories")]
	public IReadOnlyList<string> Categories { get; init; } = ["domain", "application"];

	/// <summary>
	/// Type prefixes a port must not expose. Defaults to common infrastructure technologies, because
	/// a port that names a database or an HTTP client is not a boundary, it is a leak.
	/// </summary>
	[JsonPropertyName("forbiddenTypePrefixes")]
	public IReadOnlyList<string> ForbiddenTypePrefixes { get; init; } =
	["Microsoft.EntityFrameworkCore", "System.Data", "Microsoft.AspNetCore", "System.Net.Http"];
}
