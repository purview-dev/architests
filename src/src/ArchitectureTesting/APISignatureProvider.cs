using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace Purview.Architecture.Testing;

/// <summary>Renders metadata signatures as namespace-qualified type names.</summary>
/// <remarks>
/// The reader needs names, not <see cref="Type"/> objects: the whole point is that nothing is loaded
/// or executed. Generic arguments are rendered inline, as in <c>IReadOnlyList&lt;System.String&gt;</c>,
/// so a rule can see the shape of an exposed collection without resolving it.
/// </remarks>
sealed class APISignatureProvider : ISignatureTypeProvider<string, object?>, ICustomAttributeTypeProvider<string>
{
	/// <summary>The shared instance: the provider carries no state.</summary>
	public static readonly APISignatureProvider Instance = new();

	/// <inheritdoc />
	public string GetArrayType(string elementType, ArrayShape shape) =>
		$"{elementType}[{new string(',', Math.Max(0, shape.Rank - 1))}]";

	/// <inheritdoc />
	public string GetByReferenceType(string elementType) => $"{elementType}&";

	/// <inheritdoc />
	public string GetFunctionPointerType(MethodSignature<string> signature) => "method*";

	/// <inheritdoc />
	public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
		$"{TrimArity(genericType)}<{string.Join(", ", typeArguments)}>";

	/// <inheritdoc />
	public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";

	/// <inheritdoc />
	public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";

	/// <inheritdoc />
	public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

	/// <inheritdoc />
	public string GetPinnedType(string elementType) => elementType;

	/// <inheritdoc />
	public string GetPointerType(string elementType) => $"{elementType}*";

	/// <inheritdoc />
	public string GetPrimitiveType(PrimitiveTypeCode typeCode) =>
		typeCode switch
		{
			PrimitiveTypeCode.Boolean => "System.Boolean",
			PrimitiveTypeCode.Byte => "System.Byte",
			PrimitiveTypeCode.Char => "System.Char",
			PrimitiveTypeCode.Double => "System.Double",
			PrimitiveTypeCode.Int16 => "System.Int16",
			PrimitiveTypeCode.Int32 => "System.Int32",
			PrimitiveTypeCode.Int64 => "System.Int64",
			PrimitiveTypeCode.IntPtr => "System.IntPtr",
			PrimitiveTypeCode.Object => "System.Object",
			PrimitiveTypeCode.SByte => "System.SByte",
			PrimitiveTypeCode.Single => "System.Single",
			PrimitiveTypeCode.String => "System.String",
			PrimitiveTypeCode.TypedReference => "System.TypedReference",
			PrimitiveTypeCode.UInt16 => "System.UInt16",
			PrimitiveTypeCode.UInt32 => "System.UInt32",
			PrimitiveTypeCode.UInt64 => "System.UInt64",
			PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
			PrimitiveTypeCode.Void => "System.Void",
			_ => typeCode.ToString(),
		};

	/// <inheritdoc />
	public string GetSZArrayType(string elementType) => $"{elementType}[]";

	/// <inheritdoc />
	public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
		MetadataNames.Definition(reader, handle);

	/// <inheritdoc />
	public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
		MetadataNames.Reference(reader, handle);

	/// <inheritdoc />
	public string GetTypeFromSpecification(
		MetadataReader reader,
		object? genericContext,
		TypeSpecificationHandle handle,
		byte rawTypeKind
	) => reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

	/// <inheritdoc />
	public string GetSystemType() => "System.Type";

	/// <inheritdoc />
	public string GetTypeFromSerializedName(string name)
	{
		// A serialized attribute argument is assembly-qualified, e.g. "Ns.Type, Assembly".
		var comma = name.IndexOf(',', StringComparison.Ordinal);
		return (comma < 0 ? name : name[..comma]).Replace('+', '.');
	}

	/// <inheritdoc />
	public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;

	/// <inheritdoc />
	public bool IsSystemType(string type) => string.Equals(type, "System.Type", StringComparison.Ordinal);

	/// <summary>Removes the generic arity suffix the compiler puts in a metadata name.</summary>
	/// <param name="name">The metadata name.</param>
	/// <returns>The name without <c>`n</c>.</returns>
	public static string TrimArity(string name)
	{
		var arity = name.IndexOf('`', StringComparison.Ordinal);
		return arity < 0 ? name : name[..arity];
	}
}
