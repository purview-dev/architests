using Purview.Architecture.Domain;

namespace Purview.Architecture.Application;

/// <summary>
/// Coordinates placing an order. Application depends on the domain's port, never on an adapter.
/// </summary>
/// <remarks>Creates the handler over the domain's storage port.</remarks>
/// <param name="repository">Where placed orders are stored.</param>
public sealed class PlaceOrderHandler(IOrderRepository repository)
{
	/// <summary>Places an order for the given total and stores it.</summary>
	/// <param name="total">The order total.</param>
	/// <returns>The placed order.</returns>
	public Order Place(decimal total)
	{
		Order order = new(Guid.NewGuid(), total);
		repository.Save(order);

		return order;
	}
}
