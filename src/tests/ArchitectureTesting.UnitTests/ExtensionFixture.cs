namespace Purview.Architecture.Testing.UnitTests;

/// <summary>
/// A C# extension block, so the metadata reader is tested against a real synthesized container.
/// </summary>
/// <remarks>
/// The compiler emits a nested <c>&lt;G&gt;$&lt;hash&gt;</c> container for the block. It carries no
/// attributes and no namespace, so neither <c>[CompilerGenerated]</c> nor <c>[GeneratedCode]</c>
/// identifies it - which is exactly how it reached a consumer's <c>typeShape</c> and
/// <c>typeNamespace</c> results as an unactionable violation.
/// </remarks>
public static class ExtensionFixture
{
	extension(string value)
	{
		/// <summary>Twice the length of the string, which exists only to make the block non-empty.</summary>
		public int DoubledLength => value.Length * 2;
	}
}
