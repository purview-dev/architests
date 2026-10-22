using System.Text.Json.Serialization;

namespace Purview.Architecture.Testing;

/// <summary>A ratchet on the project references each project is allowed to carry.</summary>
/// <remarks>
/// The budget records what the graph looks like today so a new dependency cannot be added without a
/// reviewed change. With <see cref="FailOnStale"/> on, a dependency that disappears must be removed
/// from the budget too: the budget only ever tightens, which is what makes it a ratchet rather than
/// a snapshot.
/// </remarks>
public sealed class DependencyBudget
{
	/// <summary>Whether the budget is enforced.</summary>
	[JsonPropertyName("enabled")]
	public bool Enabled { get; init; }

	/// <summary>
	/// Whether a budget entry listing a reference the project no longer declares is a failure.
	/// Defaults to true: a stale allowance is a permission nobody needs.
	/// </summary>
	[JsonPropertyName("failOnStale")]
	public bool FailOnStale { get; init; } = true;

	/// <summary>The per-project allowances. A project with no entry is not budgeted.</summary>
	[JsonPropertyName("entries")]
	public IReadOnlyList<DependencyBudgetEntry> Entries { get; init; } = [];
}

/// <summary>The project references one project is allowed to declare.</summary>
public sealed class DependencyBudgetEntry
{
	/// <summary>The project name, exactly as it appears on disk.</summary>
	[JsonPropertyName("project")]
	public string Project { get; init; } = string.Empty;

	/// <summary>The project references the budget permits.</summary>
	[JsonPropertyName("references")]
	public IReadOnlyList<string> References { get; init; } = [];
}
