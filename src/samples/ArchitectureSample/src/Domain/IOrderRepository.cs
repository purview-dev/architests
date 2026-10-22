namespace Purview.Architecture.Domain;

/// <summary>
/// The port through which orders are stored. The domain owns the contract, and an adapter outside
/// the domain implements it; the signature stays technology-neutral.
/// </summary>
public interface IOrderRepository
{
	/// <summary>Stores an order.</summary>
	/// <param name="order">The order to store.</param>
	void Save(Order order);

	/// <summary>Returns every stored order.</summary>
	/// <returns>The orders as a read-only list.</returns>
	IReadOnlyList<Order> All();
}
