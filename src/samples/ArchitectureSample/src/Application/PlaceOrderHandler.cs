using Purview.Architecture.Domain;

namespace Purview.Architecture.Application;

/// <summary>Coordinates placing an order. Application may depend on the domain.</summary>
public sealed class PlaceOrderHandler
{
	/// <summary>Places an order for the given total.</summary>
	/// <param name="total">The order total.</param>
	/// <returns>The placed order.</returns>
	public Order Place(decimal total) => new(Guid.NewGuid(), total);
}
