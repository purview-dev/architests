using Purview.Architecture.Application;
using Purview.Architecture.Persistence;

namespace Purview.Architecture.Composition;

/// <summary>
/// The composition root: the one place that selects and wires the concrete implementations.
/// </summary>
public sealed class CompositionRoot
{
	/// <summary>The wired handler.</summary>
	public PlaceOrderHandler Handler { get; } = new();

	/// <summary>The wired repository.</summary>
	public OrderRepository Repository { get; } = new();

	/// <summary>Places an order and saves it.</summary>
	/// <param name="total">The order total.</param>
	/// <returns>The placed order.</returns>
	public Domain.Order PlaceAndSave(decimal total)
	{
		var order = Handler.Place(total);
		Repository.Save(order);

		return order;
	}
}
