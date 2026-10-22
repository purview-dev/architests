using System.Reflection.Metadata;

namespace Purview.Architecture.Testing;

/// <summary>Reads custom attributes from metadata.</summary>
public static partial class APISurface
{
	/// <summary>Reads a collection of attributes.</summary>
	/// <param name="reader">The assembly's metadata.</param>
	/// <param name="handles">The attribute handles.</param>
	/// <returns>The attributes, in metadata order.</returns>
	static List<APIAttribute> ReadAttributes(MetadataReader reader, CustomAttributeHandleCollection handles)
	{
		List<APIAttribute> attributes = [];

		foreach (var handle in handles)
		{
			var attribute = reader.GetCustomAttribute(handle);
			string? argument = null;

			try
			{
				argument = attribute
					.DecodeValue(APISignatureProvider.Instance)
					.FixedArguments.Select(fixedArgument => fixedArgument.Value)
					.OfType<string>()
					.FirstOrDefault();
			}
			catch (BadImageFormatException)
			{
				// An attribute whose blob this reader cannot decode still has a name, and the name is
				// what the policy governs; the argument is a bonus.
			}

			attributes.Add(new APIAttribute(AttributeTypeName(reader, attribute) ?? string.Empty, argument));
		}

		return attributes;
	}

	/// <summary>Resolves the type of an attribute from its constructor.</summary>
	/// <param name="reader">The assembly's metadata.</param>
	/// <param name="attribute">The attribute.</param>
	/// <returns>The attribute type's name, or <see langword="null"/>.</returns>
	static string? AttributeTypeName(MetadataReader reader, CustomAttribute attribute)
	{
#pragma warning disable IDE0072 // Add missing cases
		var declaring = attribute.Constructor.Kind switch
		{
			HandleKind.MethodDefinition => reader
				.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor)
				.GetDeclaringType(),
			HandleKind.MemberReference => reader
				.GetMemberReference((MemberReferenceHandle)attribute.Constructor)
				.Parent,
			_ => default,
		};
#pragma warning restore IDE0072 // Add missing cases

		return declaring.IsNil ? null : MetadataNames.Name(reader, declaring);
	}
}
