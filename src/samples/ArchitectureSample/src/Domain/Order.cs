namespace Purview.Architecture.Domain;

/// <summary>An order placed in the sample.</summary>
/// <param name="Id">The order identifier.</param>
/// <param name="Total">The order total.</param>
public sealed record Order(Guid Id, decimal Total);
