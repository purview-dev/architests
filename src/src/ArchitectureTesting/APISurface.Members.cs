using System.Reflection;
using System.Reflection.Metadata;

namespace Purview.Architecture.Testing;

/// <summary>Reads members from metadata.</summary>
public static partial class APISurface
{
	/// <summary>Reads a type's public members.</summary>
	/// <param name="reader">The assembly's metadata.</param>
	/// <param name="type">The type definition.</param>
	/// <returns>The members.</returns>
	/// <remarks>
	/// Property, event and operator accessors are represented by their property or event, so they are
	/// not listed twice. Non-public members are outside the surface a consumer can call and are
	/// skipped.
	/// </remarks>
	static List<APIMember> ReadMembers(MetadataReader reader, TypeDefinition type)
	{
		List<APIMember> members = [];

		foreach (var handle in type.GetMethods())
		{
			var method = reader.GetMethodDefinition(handle);
			var attributes = method.Attributes;
			var name = reader.GetString(method.Name);
			var isConstructor = name == ".ctor";

			if (!isConstructor && (attributes & MethodAttributes.SpecialName) != 0)
				continue;

			if ((attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public)
				continue;

			var signature = method.DecodeSignature(APISignatureProvider.Instance, null);

			members.Add(
				new APIMember(
					name,
					isConstructor ? APIMemberKind.Constructor : APIMemberKind.Method,
					signature.ReturnType,
					(attributes & MethodAttributes.Static) != 0,
					ReadAttributes(reader, method.GetCustomAttributes())
				)
				{
					Parameters = signature.ParameterTypes,
					GenericArity = method.GetGenericParameters().Count,
				}
			);
		}

		foreach (var handle in type.GetProperties())
		{
			var property = reader.GetPropertyDefinition(handle);
			var accessors = property.GetAccessors();

			if (!IsPublic(reader, accessors.Getter) && !IsPublic(reader, accessors.Setter))
				continue;

			members.Add(
				new APIMember(
					reader.GetString(property.Name),
					APIMemberKind.Property,
					property.DecodeSignature(APISignatureProvider.Instance, null).ReturnType,
					IsStatic(reader, accessors.Getter) || IsStatic(reader, accessors.Setter),
					ReadAttributes(reader, property.GetCustomAttributes())
				)
			);
		}

		foreach (var handle in type.GetFields())
		{
			var field = reader.GetFieldDefinition(handle);
			var fieldName = reader.GetString(field.Name);

			// Every enum carries this synthetic instance field; it is not a declaration.
			if (fieldName == "value__")
				continue;

			if ((field.Attributes & FieldAttributes.FieldAccessMask) != FieldAttributes.Public)
				continue;

			members.Add(
				new APIMember(
					fieldName,
					APIMemberKind.Field,
					field.DecodeSignature(APISignatureProvider.Instance, null),
					(field.Attributes & FieldAttributes.Static) != 0,
					ReadAttributes(reader, field.GetCustomAttributes())
				)
			);
		}

		foreach (var handle in type.GetEvents())
		{
			var @event = reader.GetEventDefinition(handle);
			var accessors = @event.GetAccessors();

			if (!IsPublic(reader, accessors.Adder) && !IsPublic(reader, accessors.Remover))
				continue;

			members.Add(
				new APIMember(
					reader.GetString(@event.Name),
					APIMemberKind.Event,
					MetadataNames.Name(reader, @event.Type) ?? string.Empty,
					IsStatic(reader, accessors.Adder) || IsStatic(reader, accessors.Remover),
					ReadAttributes(reader, @event.GetCustomAttributes())
				)
			);
		}

		return members;
	}

	/// <summary>Reports whether an accessor is public, treating an absent accessor as not public.</summary>
	/// <param name="reader">The assembly's metadata.</param>
	/// <param name="handle">The accessor handle, which may be nil.</param>
	/// <returns><see langword="true"/> when the accessor is public.</returns>
	static bool IsPublic(MetadataReader reader, MethodDefinitionHandle handle) =>
		!handle.IsNil
		&& (reader.GetMethodDefinition(handle).Attributes & MethodAttributes.MemberAccessMask)
			== MethodAttributes.Public;

	/// <summary>Reports whether an accessor is static, treating an absent accessor as not static.</summary>
	/// <param name="reader">The assembly's metadata.</param>
	/// <param name="handle">The accessor handle, which may be nil.</param>
	/// <returns><see langword="true"/> when the accessor is static.</returns>
	static bool IsStatic(MetadataReader reader, MethodDefinitionHandle handle) =>
		!handle.IsNil && (reader.GetMethodDefinition(handle).Attributes & MethodAttributes.Static) != 0;
}
