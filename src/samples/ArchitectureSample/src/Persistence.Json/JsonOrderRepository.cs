using System.Text.Json;
using Purview.Architecture.Domain;

namespace Purview.Architecture.Persistence.Json;

/// <summary>
/// A JSON adapter for the domain's storage port, using the base class library's serializer. Sibling
/// implementations stay independent of one another.
/// </summary>
public sealed class JsonOrderRepository : IOrderRepository
{
	readonly List<string> _documents = [];

	/// <inheritdoc />
	public void Save(Order order) => _documents.Add(JsonSerializer.Serialize(order));

	/// <inheritdoc />
	public IReadOnlyList<Order> All() =>
		[.. _documents.Select(document => JsonSerializer.Deserialize<Order>(document)!)];
}
