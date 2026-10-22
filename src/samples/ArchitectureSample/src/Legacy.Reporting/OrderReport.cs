using Purview.Architecture.Domain;

namespace Purview.Architecture.Reporting;

/// <summary>
/// Renders a plain-text report from the domain's storage port. The project is named
/// <c>Legacy.Reporting</c>; a narrow waiver maps the name onto the Reporting responsibility, so only
/// the name is excused and every dependency rule still applies.
/// </summary>
/// <remarks>Creates the report over the domain's storage port.</remarks>
/// <param name="repository">The orders to report on.</param>
public sealed class OrderReport(IOrderRepository repository)
{
	/// <summary>Renders one line per stored order.</summary>
	/// <returns>The report text.</returns>
	public string Summarise() =>
		string.Join(Environment.NewLine, repository.All().Select(order => $"{order.Id}: {order.Total}"));
}
