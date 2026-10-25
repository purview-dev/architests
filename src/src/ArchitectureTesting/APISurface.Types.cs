using System.Reflection;
using System.Reflection.Metadata;

namespace Purview.Architecture.Testing;

/// <summary>Reads type declarations from metadata.</summary>
public static partial class APISurface
{
	/// <summary>Reads one type definition.</summary>
	/// <param name="reader">The assembly's metadata.</param>
	/// <param name="handle">The type definition.</param>
	/// <param name="projectName">The project the assembly belongs to.</param>
	/// <returns>The type, or <see langword="null"/> for the module's synthetic type.</returns>
	static APIType? ReadType(MetadataReader reader, TypeDefinitionHandle handle, string projectName)
	{
		var type = reader.GetTypeDefinition(handle);
		var name = reader.GetString(type.Name);

		// <Module> holds assembly-level members, not a declaration.
		if (name == "<Module>")
			return null;

		var attributes = type.Attributes;
		var visibility = attributes & TypeAttributes.VisibilityMask;
		var isPublic = visibility is TypeAttributes.Public or TypeAttributes.NestedPublic;
		var isNested = visibility is not (TypeAttributes.Public or TypeAttributes.NotPublic);
		var baseType = MetadataNames.Name(reader, type.BaseType);

		return new APIType(
			projectName,
			NamespaceOf(reader, type),
			APISignatureProvider.TrimArity(name),
			MetadataNames.Definition(reader, handle),
			KindOf(attributes, baseType),
			isPublic,
			(attributes & TypeAttributes.Abstract) != 0,
			(attributes & TypeAttributes.Sealed) != 0,
			isNested,
			baseType,
			ReadInterfaces(reader, type),
			ReadMembers(reader, type),
			ReadAttributes(reader, type.GetCustomAttributes())
		);
	}

	/// <summary>Resolves a type's namespace, following a nested type up to its outermost declaration.</summary>
	/// <param name="reader">The assembly's metadata.</param>
	/// <param name="type">The type definition.</param>
	/// <returns>The namespace, or empty for a type declared outside one.</returns>
	/// <remarks>
	/// Metadata stores no namespace on a nested type; it belongs to the type that declares it. Read
	/// directly, every public nested type looks like it lives in the global namespace, which the
	/// namespace rule then reports as being outside its project's root.
	/// </remarks>
	static string NamespaceOf(MetadataReader reader, TypeDefinition type)
	{
		var declaring = type.GetDeclaringType();

		while (!declaring.IsNil)
		{
			type = reader.GetTypeDefinition(declaring);
			declaring = type.GetDeclaringType();
		}

		return type.Namespace.IsNil ? string.Empty : reader.GetString(type.Namespace);
	}

	/// <summary>Classifies a type from its flags and base type.</summary>
	/// <param name="attributes">The type's attributes.</param>
	/// <param name="baseType">The type's base type, when it has one.</param>
	/// <returns>The kind.</returns>
	static APITypeKind KindOf(TypeAttributes attributes, string? baseType)
	{
		if ((attributes & TypeAttributes.Interface) != 0)
			return APITypeKind.Interface;

		// A type with no base type is a module-level type, which is not a declaration.
		return baseType switch
		{
			"System.Enum" => APITypeKind.Enum,
			"System.ValueType" => APITypeKind.Struct,
			"System.MulticastDelegate" => APITypeKind.Delegate,
			_ => APITypeKind.Class,
		};
	}

	/// <summary>Reads the interfaces a type implements.</summary>
	/// <param name="reader">The assembly's metadata.</param>
	/// <param name="type">The type definition.</param>
	/// <returns>The interface names.</returns>
	static List<string> ReadInterfaces(MetadataReader reader, TypeDefinition type) =>
		[
			.. type.GetInterfaceImplementations()
				.Select(handle => reader.GetInterfaceImplementation(handle).Interface)
				.Select(handle => MetadataNames.Name(reader, handle))
				.OfType<string>(),
		];
}
