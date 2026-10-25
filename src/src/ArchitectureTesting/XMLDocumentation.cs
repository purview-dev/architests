using System.Text;
using System.Xml.Linq;

namespace Purview.Architecture.Testing;

/// <summary>Reads the XML documentation file a compiler writes beside an assembly.</summary>
/// <remarks>
/// The file is the documentation contract: a public member either has an entry or it does not. It is
/// read with <see cref="XDocument"/> rather than loaded as an assembly, and a missing file yields an
/// empty set so the rule can report that documentation was not generated at all.
/// </remarks>
public static class XMLDocumentation
{
	/// <summary>Reads the documented member identifiers for an assembly.</summary>
	/// <param name="assemblyPath">The assembly's path; the XML file is expected beside it.</param>
	/// <returns>The documented identifiers, such as <c>T:Ns.Type</c>.</returns>
	public static IReadOnlySet<string> Read(string assemblyPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

		var path = Path.ChangeExtension(assemblyPath, ".xml");

		if (!File.Exists(path))
			return new HashSet<string>(StringComparer.Ordinal);

		var document = SafeXml.Load(path, "XML documentation file");

		return document
			.Descendants("member")
			.Select(element => (string?)element.Attribute("name"))
			.Where(name => !string.IsNullOrWhiteSpace(name))
			.Select(name => name!)
			.ToHashSet(StringComparer.Ordinal);
	}

	/// <summary>Reports whether the documentation file exists for an assembly.</summary>
	/// <param name="assemblyPath">The assembly's path.</param>
	/// <returns><see langword="true"/> when an XML file sits beside the assembly.</returns>
	public static bool Exists(string assemblyPath) =>
		!string.IsNullOrWhiteSpace(assemblyPath) && File.Exists(Path.ChangeExtension(assemblyPath, ".xml"));

	/// <summary>Builds the documentation identifier of a type.</summary>
	/// <param name="type">The type.</param>
	/// <returns>The identifier.</returns>
	public static string TypeId(APIType type)
	{
		ArgumentNullException.ThrowIfNull(type);
		return $"T:{type.FullName}";
	}

	/// <summary>Builds the documentation identifier of a member.</summary>
	/// <param name="type">The declaring type.</param>
	/// <param name="member">The member.</param>
	/// <returns>The identifier.</returns>
	public static string MemberId(APIType type, APIMember member)
	{
		ArgumentNullException.ThrowIfNull(type);
		ArgumentNullException.ThrowIfNull(member);

		var parameters = string.Join(",", member.Parameters.Select(FormatType));

		return member.Kind switch
		{
			APIMemberKind.Constructor => $"M:{type.FullName}.#ctor({parameters})",
			APIMemberKind.Method => $"M:{type.FullName}.{member.Name}({parameters})",
			APIMemberKind.Property => $"P:{type.FullName}.{member.Name}",
			APIMemberKind.Field => $"F:{type.FullName}.{member.Name}",
			APIMemberKind.Event => $"E:{type.FullName}.{member.Name}",
			_ => $"M:{type.FullName}.{member.Name}({parameters})",
		};
	}

	/// <summary>Converts a signature type name to the documentation identifier's notation.</summary>
	/// <param name="typeName">The signature type, as the reader renders it.</param>
	/// <returns>The documentation form.</returns>
	/// <remarks>
	/// Documentation identifiers use braces for generic arguments, <c>@</c> for a by-reference type,
	/// and backticks for generic parameters. The reader renders the CLR metadata form, so the
	/// conversion is mechanical rather than a second parse.
	/// </remarks>
	public static string FormatType(string typeName)
	{
		ArgumentNullException.ThrowIfNull(typeName);

		StringBuilder builder = new(typeName.Length);

		for (var index = 0; index < typeName.Length; index++)
		{
			switch (typeName[index])
			{
				case '<':
					builder.Append('{');
					break;

				case '>':
					builder.Append('}');
					break;

				case '&':
					builder.Append('@');
					break;

				case '!' when index + 1 < typeName.Length && typeName[index + 1] == '!':
					builder.Append("``");
					index++;
					break;

				case '!':
					builder.Append('`');
					break;

				default:
					builder.Append(typeName[index]);
					break;
			}
		}

		return builder.ToString().Replace(", ", ",", StringComparison.Ordinal);
	}
}
