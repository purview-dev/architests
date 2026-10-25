namespace Purview.Architecture.Domain;

/// <summary>Conveniences over <see cref="Order"/>, written as a C# extension block.</summary>
/// <remarks>
/// The sample uses an extension block deliberately. The compiler emits a nested container whose
/// name begins with <c>&lt;</c> and which carries no attributes, so it is invisible to an
/// attribute-only generated-code check. The type rules skip it as compiler-synthesized, and this
/// project is governed by <c>typeRules</c>, so the sample suite is the end-to-end guard that they
/// still do.
/// </remarks>
public static class OrderExtensions
{
	extension(Order order)
	{
		/// <summary>Whether the order has nothing to settle.</summary>
		public bool IsFree => order.Total == 0m;
	}
}
