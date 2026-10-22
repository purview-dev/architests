using Purview.Architecture.Application;
using Purview.Architecture.Domain;
using Purview.Architecture.Persistence.InMemory;

namespace Purview.Architecture.Composition;

/// <summary>
/// The composition root: the one place that selects an adapter and wires the handler. Composition
/// may depend on every layer, and no layer may depend on it.
/// </summary>
public sealed class CompositionRoot
{
	readonly PlaceOrderHandler _handler;

	/// <summary>Wires the in-memory adapter into the application handler.</summary>
	public CompositionRoot() => _handler = new PlaceOrderHandler(new InMemoryOrderRepository());

	/// <summary>Places an order and stores it through the wired adapter.</summary>
	/// <param name="total">The order total.</param>
	/// <returns>The placed order.</returns>
	public Order PlaceAndSave(decimal total) => _handler.Place(total);
}
