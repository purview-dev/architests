using BDTest;

namespace Purview.Architecture.Testing.AcceptanceTests;

/// <summary>The "given" steps: what the repository declares.</summary>
public sealed class RepositorySteps(PolicyContext context)
{
	/// <summary>The hash the compiler appends to an extension container's name.</summary>
	/// <remarks>Any hash does; what matters is the reserved <c>&lt;</c> the name starts with.</remarks>
	const string Hash = "7A6AE160A7649B5FD624AE28A23FFA64";

	[StepText("a project governed by the type rules")]
	public void AGovernedProject() => context.TypeRules = new TypeRules { Enabled = true };

	[StepText("a project governed by the type rules, with generated code not excluded")]
	public void AGovernedProjectIncludingGeneratedCode() =>
		context.TypeRules = new TypeRules { Enabled = true, ExcludeGeneratedCode = false };

	[StepText("it declares a C# extension block on the static class {name}")]
	public void ItDeclaresAnExtensionBlock(string name) => DeclareExtensionBlock(name, PolicyContext.RootNamespace);

	[StepText("it declares a C# extension block on the static class {name} in namespace {ns}")]
	public void ItDeclaresAnExtensionBlockIn(string name, string ns) => DeclareExtensionBlock(name, ns);

	[StepText("it declares the sealed public class {name}")]
	public void ItDeclaresASealedClass(string name) => context.Types.Add(Fixture.Type(name));

	[StepText("it declares the unsealed public class {name}")]
	public void ItDeclaresAnUnsealedClass(string name) => context.Types.Add(Fixture.Type(name, isSealed: false));

	[StepText("it declares the public interface {name}")]
	public void ItDeclaresAnInterface(string name) =>
		context.Types.Add(Fixture.Type(name, kind: APITypeKind.Interface));

	[StepText("it declares the public class {name} in namespace {ns}")]
	public void ItDeclaresAClassIn(string name, string ns) => context.Types.Add(Fixture.Type(name, ns: ns));

	[StepText("it declares the public class {name} nested inside {outer}, which is in namespace {ns}")]
	public void ItDeclaresANestedClass(string name, string outer, string ns)
	{
		context.Types.Add(Fixture.Type(outer, ns: ns));

		// Metadata records no namespace on a nested type; the reader resolves it from the outer
		// type, so the fixture models the resolved value.
		context.Types.Add(Fixture.Type(name, ns: ns, fullName: $"{ns}.{outer}.{name}", isNested: true));
	}

	/// <summary>
	/// Declares the author-written static class and the container the compiler emits beneath it.
	/// </summary>
	/// <param name="name">The static class the extension block is written in.</param>
	/// <param name="ns">The namespace the static class is declared in.</param>
	/// <remarks>
	/// The container carries <b>no attributes at all</b> - not <c>[CompilerGenerated]</c>, not
	/// <c>[GeneratedCode]</c> - which is precisely why an attribute-only generated-code check misses
	/// it. It is unsealed and has only static members, so it looks exactly like a utility class that
	/// forgot to be static.
	/// </remarks>
	void DeclareExtensionBlock(string name, string ns)
	{
		context.Types.Add(Fixture.Type(name, ns: ns, isAbstract: true, isSealed: true));

		context.Types.Add(
			Fixture.Type(
				$"<G>${Hash}",
				ns: ns,
				fullName: $"{ns}.{name}.<G>${Hash}`1",
				isNested: true,
				isSealed: false,
				members: [Fixture.Member("DoubledLength", typeName: "System.Int32", isStatic: true)]
			)
		);
	}
}
