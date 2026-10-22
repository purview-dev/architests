using Purview.Architecture.Domain;

namespace Purview.Architecture.Persistence.InMemory;

/// <summary>
/// An in-memory adapter for the domain's storage port. It is one of two sibling implementations,
/// which is why the Persistence responsibility sets siblingIndependence.
/// </summary>
public sealed class InMemoryOrderRepository : IOrderRepository
{
	readonly List<Order> _orders = [];

	/// <inheritdoc />
	public void Save(Order order) => _orders.Add(order);

	/// <inheritdoc />
	public IReadOnlyList<Order> All() => _orders;
}
