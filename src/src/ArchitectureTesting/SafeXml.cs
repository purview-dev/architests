using System.Xml;
using System.Xml.Linq;

namespace Purview.Architecture.Testing;

/// <summary>Loads XML with the protections this package depends on asserted rather than assumed.</summary>
/// <remarks>
/// Both XML inputs - a <c>.csproj</c> and a compiler-written documentation file - are ordinary
/// repository content, but the reader walks whatever the source roots contain and does not verify
/// that a <c>.csproj</c> was written by MSBuild.
/// <para>
/// <see cref="XDocument.Load(string)"/> is already safe on .NET Core, where DTD processing is
/// prohibited and no resolver is configured. That safety is inherited from a framework default,
/// though, which is the kind of thing a future default can quietly change. Stating it here makes
/// the guarantee this package relies on visible and testable.
/// </para>
/// </remarks>
static class SafeXml
{
	static readonly XmlReaderSettings Settings = new()
	{
		// No external entities and no entity expansion: no XXE, no billion laughs.
		DtdProcessing = DtdProcessing.Prohibit,
		XmlResolver = null,
		IgnoreComments = true,
		IgnoreWhitespace = true,
		CloseInput = true,
	};

	/// <summary>Loads an XML file, reporting a malformed one against the file that caused it.</summary>
	/// <param name="path">The file to load.</param>
	/// <param name="description">What the file is, used in the failure message.</param>
	/// <returns>The document.</returns>
	/// <exception cref="InvalidOperationException">The file is not well-formed XML.</exception>
	public static XDocument Load(string path, string description)
	{
		try
		{
			using var reader = XmlReader.Create(path, Settings);

			return XDocument.Load(reader);
		}
		catch (XmlException exception)
		{
			// Unhandled, a single malformed file aborts the whole evaluation with a stack trace
			// that never names it, leaving the reader to find it by bisection.
			throw new InvalidOperationException(
				$"The {description} at '{path}' is not well-formed XML: {exception.Message}",
				exception
			);
		}
	}
}
