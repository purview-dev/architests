using System.Reflection.Metadata;

namespace Purview.Architecture.Testing;

/// <summary>Resolves metadata handles to namespace-qualified names.</summary>
static class MetadataNames
{
	/// <summary>Renders a type definition's name, including its declaring type.</summary>
	/// <param name="reader">The assembly's metadata.</param>
	/// <param name="handle">The type definition.</param>
	/// <returns>The full name.</returns>
	public static string Definition(MetadataReader reader, TypeDefinitionHandle handle)
	{
		var type = reader.GetTypeDefinition(handle);
		var name = reader.GetString(type.Name);
		var declaring = type.GetDeclaringType();

		if (!declaring.IsNil)
			return $"{Definition(reader, declaring)}.{name}";

		// A type definition's namespace is empty when it is nested, so the namespace is only used for top-level types.
		return type.Namespace.IsNil ? name : $"{reader.GetString(type.Namespace)}.{name}";
	}

	/// <summary>Renders a type reference's name, including its declaring type.</summary>
	/// <param name="reader">The assembly's metadata.</param>
	/// <param name="handle">The type reference.</param>
	/// <returns>The full name.</returns>
	public static string Reference(MetadataReader reader, TypeReferenceHandle handle)
	{
		var type = reader.GetTypeReference(handle);
		var name = reader.GetString(type.Name);

		if (type.ResolutionScope.Kind == HandleKind.TypeReference)
			return $"{Reference(reader, (TypeReferenceHandle)type.ResolutionScope)}.{name}";

		// A type reference's namespace is empty when it is nested, so the namespace is only used for top-level types.
		return type.Namespace.IsNil ? name : $"{reader.GetString(type.Namespace)}.{name}";
	}

	/// <summary>Renders any type handle as a name.</summary>
	/// <param name="reader">The assembly's metadata.</param>
	/// <param name="handle">The handle, which may be nil.</param>
	/// <returns>The name, or <see langword="null"/> when the handle is not a type.</returns>
	/// <remarks>
	/// A nil handle is normal: an interface and <c>System.Object</c> have no base type. Reading
	/// <see cref="EntityHandle.Kind"/> on a nil handle throws, so it is checked first.
	/// </remarks>
	public static string? Name(MetadataReader reader, EntityHandle handle)
	{
		if (handle.IsNil)
			return null;

		// The handle is a type definition, reference or specification. A specification is a generic
#pragma warning disable IDE0072 // Add missing cases
		return handle.Kind switch
		{
			HandleKind.TypeDefinition => Definition(reader, (TypeDefinitionHandle)handle),
			HandleKind.TypeReference => Reference(reader, (TypeReferenceHandle)handle),
			HandleKind.TypeSpecification => reader
				.GetTypeSpecification((TypeSpecificationHandle)handle)
				.DecodeSignature(APISignatureProvider.Instance, null),
			_ => null,
		};
#pragma warning restore IDE0072 // Add missing cases
	}
}
