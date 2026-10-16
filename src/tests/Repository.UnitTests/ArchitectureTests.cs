using Purview.Architecture.Testing;

namespace Purview.Architecture.Repository;

/// <summary>
/// Runs this repository's own architecture policy - through the package it ships - against this
/// repository.
/// </summary>
/// <remarks>
/// The policy lives at <c>.config/architecture-policy.json</c> and resolves by the default marker
/// walk, so the dogfood path is exactly the one a consumer takes. It enables
/// <c>assemblyRules</c>, so the type-level rule is exercised against the compiled package assembly
/// on every run.
/// </remarks>
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests;
