using Purview.Architecture.Domain;

namespace Purview.Architecture.Persistence;

/// <summary>Stores orders. Persistence may depend on the domain but not on the application.</summary>
public sealed class OrderRepository
{
	readonly List<Order> _orders = [];

	/// <summary>Saves an order.</summary>
	/// <param name="order">The order to save.</param>
	public void Save(Order order) => _orders.Add(order);

	/// <summary>Returns every saved order.</summary>
	/// <returns>The saved orders.</returns>
	public IReadOnlyList<Order> All() => _orders;
}
