using Purview.Architecture.Testing;

namespace Purview.Architecture.Sample;

/// <summary>
/// The sample's entire architecture suite: one derived class.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>[InheritsTests]</c> is load-bearing.</b> TUnit does not walk base types when it discovers
/// tests, so without it this class compiles, the run is green, and nothing is enforced.
/// </para>
/// <para>
/// There is no <c>RepositoryRoot</c> override: the default probe walks up from this test binary and
/// finds the sample's own <c>.config/architecture-policy.json</c> before the repository root's, so
/// the sample is a self-contained repository inside the repository.
/// </para>
/// </remarks>
[InheritsTests]
public sealed class ArchitectureTests : PurviewArchitectureTests;
