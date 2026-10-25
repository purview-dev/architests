using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Purview.Architecture.Testing.UnitTests;

/// <summary>
/// Exercises the metadata reader against an assembly that is valid enough to open but malformed
/// inside.
/// </summary>
/// <remarks>
/// The reader walks every <c>*.dll</c> beneath a configured root, and nothing guarantees those
/// files were produced by a compiler. A type reference's resolution scope may be another type
/// reference - that is how a nested type is expressed - and nothing in the format prevents that
/// chain from pointing back at itself.
/// </remarks>
public sealed class MalformedAssemblyTests
{
	/// <summary>
	/// The unhappy path: two type references whose scopes point at each other. Walking the chain
	/// without a bound never terminates, so this test hangs rather than fails if the bound is
	/// removed.
	/// </summary>
	[Test]
	[Timeout(30_000)]
	public async Task Load_GivenACyclicTypeReferenceScope_Terminates(CancellationToken cancellationToken)
	{
		// Arrange
		var directory = Directory.CreateTempSubdirectory("PurviewMalformedPE");

		try
		{
			var path = Path.Combine(directory.FullName, "Cyclic.dll");
			await File.WriteAllBytesAsync(path, BuildAssemblyWithCyclicScope(), cancellationToken);

			Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
			{
				["Cyclic"] = new("Cyclic", "/repo/src/Cyclic/Cyclic.csproj", [], [], false),
			};

			// Act
			var assemblies = AssemblyGraph.Load([path], projects);

			// Assert
			await Assert
				.That(assemblies.ContainsKey("Cyclic"))
				.IsTrue()
				.Because("the assembly is readable; only the scope chain inside it is broken");

			await Assert
				.That(assemblies["Cyclic"].References)
				.IsEmpty()
				.Because("a scope that never resolves to an assembly contributes no dependency");
		}
		finally
		{
			Directory.Delete(directory.FullName, recursive: true);
		}
	}

	/// <summary>The happy path: an ordinary assembly is still read, and its references resolved.</summary>
	[Test]
	public async Task Load_GivenARealAssembly_ResolvesItsReferences()
	{
		// Arrange
		var path = typeof(ArchitecturePolicy).Assembly.Location;

		Dictionary<string, ProjectNode> projects = new(StringComparer.OrdinalIgnoreCase)
		{
			["ArchitectureTesting"] = new("ArchitectureTesting", "/repo/src/A/A.csproj", [], [], false),
		};

		// Act
		var assemblies = AssemblyGraph.Load([path], projects);

		// Assert
		await Assert.That(assemblies.ContainsKey("ArchitectureTesting")).IsTrue();
	}

	/// <summary>
	/// Emits a minimal managed assembly whose two type references name each other as their
	/// resolution scope.
	/// </summary>
	/// <returns>The assembly bytes.</returns>
	static byte[] BuildAssemblyWithCyclicScope()
	{
		MetadataBuilder metadata = new();

		metadata.AddAssembly(
			metadata.GetOrAddString("Cyclic"),
			new Version(1, 0, 0, 0),
			default,
			default,
			0,
			AssemblyHashAlgorithm.Sha1
		);

		metadata.AddModule(
			0,
			metadata.GetOrAddString("Cyclic.dll"),
			metadata.GetOrAddGuid(Guid.NewGuid()),
			default,
			default
		);

		// The module's synthetic type, which every assembly carries.
		metadata.AddTypeDefinition(
			default,
			default,
			metadata.GetOrAddString("<Module>"),
			default,
			MetadataTokens.FieldDefinitionHandle(1),
			MetadataTokens.MethodDefinitionHandle(1)
		);

		// Rows 1 and 2 of the TypeReference table, each naming the other as its scope. The handles
		// are plain tokens, so row 1 can reference row 2 before row 2 is written.
		var first = MetadataTokens.TypeReferenceHandle(1);
		var second = MetadataTokens.TypeReferenceHandle(2);

		metadata.AddTypeReference(second, metadata.GetOrAddString("Cyclic"), metadata.GetOrAddString("First"));
		metadata.AddTypeReference(first, metadata.GetOrAddString("Cyclic"), metadata.GetOrAddString("Second"));

		ManagedPEBuilder peBuilder = new(
			PEHeaderBuilder.CreateLibraryHeader(),
			new MetadataRootBuilder(metadata),
			new BlobBuilder()
		);

		BlobBuilder peImage = new();
		peBuilder.Serialize(peImage);

		return [.. peImage.ToImmutableArray()];
	}
}
