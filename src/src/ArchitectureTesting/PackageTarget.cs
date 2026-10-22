namespace Purview.Architecture.Testing;

/// <summary>One target framework's package graph, as restore resolved it.</summary>
/// <param name="Framework">The target framework key, such as <c>net10.0</c>.</param>
/// <param name="Direct">The packages the project declares directly, including a centrally injected one.</param>
/// <param name="Owned">
/// The direct packages and the packages they pull in. This is the part of the graph the project
/// chose, before a referenced project's packages are considered.
/// </param>
/// <param name="Resolved">Every package in the target's restore closure.</param>
/// <param name="ThroughReferences">
/// The package closure each referenced project contributes, keyed by project name. A permitted
/// project reference explains these packages, so they are not the referencing project's own.
/// </param>
/// <remarks>
/// NuGet writes one target per framework (and per runtime identifier). The ownership analysis is
/// per target, because a conditional reference exists in one target and not another.
/// </remarks>
public sealed record PackageTarget(
	string Framework,
	IReadOnlyList<string> Direct,
	IReadOnlyList<string> Owned,
	IReadOnlyList<string> Resolved,
	IReadOnlyDictionary<string, IReadOnlyList<string>> ThroughReferences
);
